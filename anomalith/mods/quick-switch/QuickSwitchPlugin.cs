using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace AnomalithQuickSwitch
{
#if ANOMALITH_IL2CPP
    [BepInPlugin("labs.anomalith.quickswitch", "ANOMALITH Quick Weapon Switch", "1.0.3")]
#if DEBUG
    public sealed class PluginLoader : Il2CppDebugHost<DevelopmentDriver, QuickSwitchPlugin>
    {
    }

    public sealed class DevelopmentDriver : UnityEngine.MonoBehaviour
    {
        public DevelopmentDriver(System.IntPtr pointer) : base(pointer)
        {
        }

        private void Update() => Il2CppDebugSession.Current?.Update();
        private void LateUpdate() => Il2CppDebugSession.Current?.LateUpdate();
        private void OnGUI() => Il2CppDebugSession.Current?.Draw();
        private void OnDestroy() => Il2CppDebugSession.Current?.Dispose();
    }

    public sealed class QuickSwitchPlugin : Il2CppDebugFeature
    {
#else
    public sealed class PluginLoader : Il2CppPluginHost<QuickSwitchPlugin>
    {
    }

    public sealed class QuickSwitchPlugin : UnityEngine.MonoBehaviour
    {
        public QuickSwitchPlugin(System.IntPtr pointer) : base(pointer)
        {
        }

        private ConfigFile Config => Il2CppPluginContext.Config;
        private BepInEx.Logging.ManualLogSource Logger => Il2CppPluginContext.Log;
#endif
#else
    [BepInPlugin(PluginId, "ANOMALITH Quick Weapon Switch", "1.0.3")]
    public sealed class QuickSwitchPlugin : BaseUnityPlugin
    {
#endif
        private const string PluginId = "labs.anomalith.quickswitch";
        private static readonly WeaponShortcutUI.Arrow[] ScrollOrder =
        {
            WeaponShortcutUI.Arrow.Left, WeaponShortcutUI.Arrow.Up, WeaponShortcutUI.Arrow.Right
        };
        internal const int MinimumSpeed = 10;
        internal const int DefaultSpeed = 15;
        internal const int MaximumSpeed = 30;
        private static ConfigEntry<bool> quickSwitch;
        private static ConfigEntry<int> switchSpeed;
        private Harmony harmony;
        private SwitchOption option;

#if ANOMALITH_IL2CPP
        private void Awake()
#else
        private IEnumerator Start()
#endif
        {
#if !ANOMALITH_IL2CPP
            // ScriptEngine destroys the old component at the end of the frame.
            yield return null;
#endif
            quickSwitch = Config.Bind("Gameplay", "Enabled", true, "Speed up normal firearm swaps.");
            switchSpeed = Config.Bind("Gameplay", "SpeedTenths", DefaultSpeed, new ConfigDescription(
                "Weapon switch speed in tenths of the normal speed. 15 means 1.5×.",
                new AcceptableValueRange<int>(MinimumSpeed, MaximumSpeed)));
            harmony = new Harmony($"{PluginId}.{GetInstanceID()}");
            try
            {
                var swap = AccessTools.DeclaredMethod(typeof(PlayerCtrl), nameof(PlayerCtrl.ChangeWeapon), Type.EmptyTypes);
#if ANOMALITH_IL2CPP
                var shortcut = AccessTools.DeclaredMethod(typeof(Inventory), nameof(Inventory.GetCurrentWeapon),
                    new[] { typeof(WeaponShortcutUI.Arrow), typeof(bool) });
                var speed = AccessTools.DeclaredMethod(typeof(WeaponBase), nameof(WeaponBase.GetChangeSpd));
                if (swap == null || shortcut == null || speed == null)
                    throw new InvalidOperationException("The native weapon switch is unavailable.");
                harmony.Patch(swap, prefix: PatchMethod(nameof(BeginSwap)), finalizer: PatchMethod(nameof(EndSwap)));
                harmony.Patch(shortcut, prefix: PatchMethod(nameof(SelectScrollWeapon)));
                harmony.Patch(speed, postfix: PatchMethod(nameof(ApplySwapSpeed)));
#else
                harmony.Patch(swap, transpiler: new HarmonyMethod(
                    AccessTools.DeclaredMethod(typeof(QuickSwitchPlugin), nameof(PatchSwap))));
#endif
                option = new SwitchOption(quickSwitch, switchSpeed, Logger);
                Logger.LogInfo($"Quick Weapon Switch loaded. Enabled: {quickSwitch.Value}.");
            }
            catch (Exception error)
            {
                harmony.UnpatchSelf();
                Logger.LogWarning($"Could not attach Quick Weapon Switch. {error.Message}");
            }
        }

        private void LateUpdate()
        {
            option?.Refresh();
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
            option?.Dispose();
        }

#if ANOMALITH_IL2CPP
        [ThreadStatic]
        private static SwapContext activeSwap;

        private static HarmonyMethod PatchMethod(string name) =>
            new HarmonyMethod(AccessTools.DeclaredMethod(typeof(QuickSwitchPlugin), name));

        private static void BeginSwap(PlayerCtrl __instance, out SwapContext __state)
        {
            __state = activeSwap;
            activeSwap = default;
            var battle = BattleManager.Instance;
            if (quickSwitch?.Value != true || battle == null || battle.GetCurrentPlayer() != __instance ||
                !__instance.HasInput())
                return;

            bool direct = __instance.GetUp() || __instance.GetLeft() || __instance.GetRight() ||
                __instance.GetOtherShortcut_Left() || __instance.GetOtherShortcut_Up() ||
                __instance.GetOtherShortcut_Right();
            float scroll = __instance.GetMouseScroll().y;
            bool cycle = !direct && scroll != 0f && WeaponShortcutUI.GetDialog() == null &&
                (DialogManager.Instance == null || !DialogManager.IsOpenDialog);
            activeSwap = new SwapContext(__instance, cycle ? scroll : 0f);
        }

        private static bool SelectScrollWeapon(Inventory __instance, ref WeaponBase __result)
        {
            if (activeSwap.Scroll == 0f)
                return true;

            // The native lookup opens the wheel before it returns a weapon.
            __result = GetScrollWeapon(__instance, activeSwap.Player, activeSwap.Scroll);
            return false;
        }

        private static void ApplySwapSpeed(WeaponBase __instance, ref float __result)
        {
            if (activeSwap.Player != null)
                __result = ScaleSwapSpeed(__instance, activeSwap.Player, __result);
        }

        private static Exception EndSwap(Exception __exception, SwapContext __state)
        {
            activeSwap = __state;
            return __exception;
        }

        private readonly struct SwapContext
        {
            internal readonly PlayerCtrl Player;
            internal readonly float Scroll;

            internal SwapContext(PlayerCtrl player, float scroll)
            {
                Player = player;
                Scroll = scroll;
            }
        }
#else
        private static IEnumerable<CodeInstruction> PatchSwap(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var getter = AccessTools.DeclaredMethod(typeof(WeaponBase), nameof(WeaponBase.GetChangeSpd));
            int speedIndex = FindCall(code, getter);

            // Replace the speed lookup inside the voluntary swap only. The game still sets
            // GUN_CHANGE_SPD and runs the original transition and readiness checks.
            code[speedIndex].opcode = OpCodes.Ldarg_0;
            code[speedIndex].operand = null;
            code.Insert(speedIndex + 1, new CodeInstruction(OpCodes.Call,
                AccessTools.DeclaredMethod(typeof(QuickSwitchPlugin), nameof(GetSwapSpeed))));

            int scrollStart = FindCall(code, AccessTools.DeclaredMethod(
                typeof(SaveDataLoadout), nameof(SaveDataLoadout.GetCurrentWeaponArrow))) - 1;
            int scrollEnd = code.FindIndex(scrollStart + 1, instruction =>
                instruction.Calls(AccessTools.DeclaredMethod(typeof(CharaObjBase), "GetUp")));
            var select = AccessTools.DeclaredMethod(typeof(Inventory), nameof(Inventory.GetCurrentWeapon),
                new[] { typeof(WeaponShortcutUI.Arrow), typeof(bool) });
            if (scrollStart < 4 || scrollEnd <= scrollStart ||
                code[scrollStart - 4].opcode != OpCodes.Ldloc_1)
                throw new InvalidOperationException("The native scroll selection path has changed.");

            var scrollSelectors = Enumerable.Range(scrollStart, scrollEnd - scrollStart)
                .Where(index => code[index].Calls(select)).ToArray();
            if (scrollSelectors.Length != 4)
                throw new InvalidOperationException("Expected four shortcut lookups in the scroll path.");

            // The last two lookups are used when the wheel is closed. Substitute a weapon
            // directly instead of the lookup that opens the wheel on the first notch.
            var directSelect = AccessTools.DeclaredMethod(typeof(QuickSwitchPlugin), nameof(SelectWithWheelClosed));
            for (int i = scrollSelectors.Length - 1; i >= 2; i--)
            {
                int index = scrollSelectors[i];
                code[index].opcode = OpCodes.Ldarg_0;
                code[index].operand = null;
                code.Insert(index + 1, new CodeInstruction(OpCodes.Ldloc_1));
                code.Insert(index + 2, new CodeInstruction(OpCodes.Call, directSelect));
            }
            return code;
        }

        private static int FindCall(List<CodeInstruction> code, MethodInfo target)
        {
            int index = code.FindIndex(instruction => instruction.Calls(target));
            if (index < 0 || code.FindLastIndex(instruction => instruction.Calls(target)) != index)
                throw new InvalidOperationException($"Expected one {target?.Name} call in PlayerCtrl.ChangeWeapon.");
            return index;
        }

        private static WeaponBase SelectWithWheelClosed(Inventory inventory, WeaponShortcutUI.Arrow arrow,
            bool outside, PlayerCtrl player, float scroll)
        {
            var battle = BattleManager.Instance;
            if (quickSwitch.Value && battle != null && battle.GetCurrentPlayer() == player &&
                !DialogManager.IsOpenDialog)
                return GetScrollWeapon(inventory, player, scroll);

            return inventory.GetCurrentWeapon(arrow, outside);
        }
#endif

        private static WeaponBase GetScrollWeapon(Inventory inventory, PlayerCtrl player, float scroll)
        {
            var loadout = SaveRootGame.Loadout;
            if (loadout == null)
                return null;

            var current = player.GetCurrentWeapon();
            var point = loadout.GetCurrentWeaponPointData();
            int start = point == null ? -1 : Array.IndexOf(ScrollOrder, point.m_ArrowType);
            if (start >= 0 && point.m_IsOutSide)
                start += ScrollOrder.Length;

            // Match the game's inner then outer shortcut order in both directions.
            int slots = 2 * ScrollOrder.Length;
            int direction = scroll > 0 ? -1 : 1;
            int slot = start < 0 && direction < 0 ? 0 : start;
            for (int i = 0; i < slots; i++)
            {
                slot = (slot + direction + slots) % slots;
                var arrow = ScrollOrder[slot % ScrollOrder.Length];
                int layer = slot / ScrollOrder.Length;
                if (layer >= loadout.GetShortcutLoadoutCount(arrow))
                    continue;

                string id = loadout.GetShortcutLoadout(arrow, layer)?.GetWeaponID();
                if (string.IsNullOrEmpty(id))
                    continue;

                var weapon = inventory.GetWeapon(id);
                if (weapon != null && weapon != current)
                    return weapon;
            }
            return null;
        }

#if !ANOMALITH_IL2CPP
        private static float GetSwapSpeed(WeaponBase weapon, PlayerCtrl player) =>
            ScaleSwapSpeed(weapon, player, weapon.GetChangeSpd());
#endif

        private static float ScaleSwapSpeed(WeaponBase weapon, PlayerCtrl player, float speed)
        {
            var battle = BattleManager.Instance;
            if (quickSwitch.Value && battle != null && battle.GetCurrentPlayer() == player &&
                weapon.GetWeaponType() != WeaponBase.WEAPON_TYPE.THROWABLE &&
                speed > 0 && !float.IsInfinity(speed))
                return speed * (switchSpeed.Value / 10f);

            return speed;
        }
    }
}
