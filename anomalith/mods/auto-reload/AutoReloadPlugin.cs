using System;
using System.Reflection;
using Anomalith;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace AnomalithAutoReload
{
#if ANOMALITH_IL2CPP
    [BepInPlugin("labs.anomalith.autoreload", "ANOMALITH Auto Reload", "1.1.1")]
#if DEBUG
    public sealed class PluginLoader : Il2CppDebugHost<DevelopmentDriver, AutoReloadPlugin>
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

    public sealed class AutoReloadPlugin : Il2CppDebugFeature
    {
#else
    public sealed class PluginLoader : Il2CppPluginHost<AutoReloadPlugin>
    {
    }

    public sealed class AutoReloadPlugin : UnityEngine.MonoBehaviour
    {
        public AutoReloadPlugin(System.IntPtr pointer) : base(pointer)
        {
        }

        private ConfigFile Config => Il2CppPluginContext.Config;
        private BepInEx.Logging.ManualLogSource Logger => Il2CppPluginContext.Log;
#endif
#else
    [BepInPlugin(PluginId, "ANOMALITH Auto Reload", "1.1.1")]
    public sealed class AutoReloadPlugin : BaseUnityPlugin
    {
#endif
        private const string PluginId = "labs.anomalith.autoreload";
        private static ConfigEntry<bool> autoReload;
        private Harmony harmony;
        private NativeToggleOption option;

        private void Awake()
        {
            autoReload = Config.Bind("Gameplay", "Enabled", true,
                "Automatically start a normal reload when the active firearm runs empty.");
            var condition = AccessTools.DeclaredMethod(typeof(CancelDriver), "HasFlag",
                new[] { typeof(CancelCommands) });
            if (condition?.ReturnType != typeof(bool))
            {
                Logger.LogWarning("The native reload condition was not found in this game build.");
                return;
            }

            // ScriptEngine can create the replacement before destroying the old component.
            harmony = new Harmony($"{PluginId}.{GetInstanceID()}");
            try
            {
                var postfix = new HarmonyMethod(typeof(AutoReloadPlugin).GetMethod(nameof(AllowEmptyReload),
                    BindingFlags.Static | BindingFlags.NonPublic));
                harmony.Patch(condition, postfix: postfix);
                option = new NativeToggleOption(autoReload, Logger, "Auto Reload", "LABS_AUTO_RELOAD");
                Logger.LogInfo($"Auto Reload loaded. Enabled: {autoReload.Value}.");
            }
            catch (Exception error)
            {
                harmony.UnpatchSelf();
                Logger.LogWarning($"Auto Reload could not attach to the native reload condition: {error.Message}");
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

        private static void AllowEmptyReload(CancelDriver __instance, CancelCommands __0, ref bool __result)
        {
            if (__result || __0 != CancelCommands.RELOAD || autoReload?.Value != true)
                return;

            var battle = BattleManager.Instance;
            if (battle == null)
                return;

            var player = __instance.GetPlayer();
            if (player == null || player != battle.GetCurrentPlayer() || player.IsDead())
                return;

            var weapon = player.GetCurrentWeapon();
            if (weapon == null || weapon.GetWeaponType() == WeaponBase.WEAPON_TYPE.THROWABLE ||
                !weapon.IsEmpty() || weapon.GetFireCoolDown() > 0)
                return;

            if (battle.IsInputStop() || battle.IsChangingArea || battle.IsCutScene() ||
                BattleManager.IsStopInput() || PauseMenu.IsOpen ||
                (PauseMenu.Instance != null && PauseMenu.Instance.IsOpenProcess()) ||
                (DialogManager.Instance != null && DialogManager.IsOpenDialog))
                return;

            // Bolt-action weapons need their native cocking animation to finish first.
            if (weapon.CockingType == WeaponBase.COCKING_TYPE.USE &&
                (player._Anim == null || !player._Anim.GetCurrentAnimatorStateInfo(3).IsName("IDLE")))
                return;

            if (Inventory.Instance != null && SaveRootGame.Flg != null && player.CanReload())
                __result = true;
        }
    }
}
