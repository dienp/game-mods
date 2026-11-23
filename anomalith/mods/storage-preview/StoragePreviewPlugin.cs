using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Anomalith;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalithStoragePreview
{
#if ANOMALITH_IL2CPP
    [BepInPlugin("labs.anomalith.storagepreview", "ANOMALITH Inventory Storage Preview", "1.0.1")]
#if DEBUG
    public sealed class PluginLoader : Il2CppDebugHost<DevelopmentDriver, StoragePreviewPlugin>
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

    public sealed class StoragePreviewPlugin : Il2CppDebugFeature
    {
#else
    public sealed class PluginLoader : Il2CppPluginHost<StoragePreviewPlugin>
    {
    }

    public sealed class StoragePreviewPlugin : UnityEngine.MonoBehaviour
    {
        public StoragePreviewPlugin(System.IntPtr pointer) : base(pointer)
        {
        }

        private ConfigFile Config => Il2CppPluginContext.Config;
        private BepInEx.Logging.ManualLogSource Logger => Il2CppPluginContext.Log;
#endif
#else
    [BepInPlugin(PluginId, "ANOMALITH Inventory Storage Preview", "1.0.1")]
    public sealed class StoragePreviewPlugin : BaseUnityPlugin
    {
#endif
        private const string PluginId = "labs.anomalith.storagepreview";
        private const string CountPrefix = "x ";
        private static readonly NativeField DetailText =
            NativeField.Find(typeof(ItemPopUp), "m_InventoryPopUpItemDeteilText",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        private static readonly Dictionary<TextMeshProUGUI, Image> Icons = new Dictionary<TextMeshProUGUI, Image>();
        private static ConfigEntry<bool> enabledPreview;
        private Harmony harmony;
        private NativeToggleOption option;

#if ANOMALITH_IL2CPP
        private void Awake()
#else
        private IEnumerator Start()
#endif
        {
#if !ANOMALITH_IL2CPP
            // Wait for ScriptEngine to remove the previous instance and its patch.
            yield return null;
#endif
            enabledPreview = Config.Bind("Inventory", "Enabled", true, "Show the Base Storage count in Inventory tooltips.");
            harmony = new Harmony($"{PluginId}.{GetInstanceID()}");
            try
            {
                var popup = AccessTools.DeclaredMethod(typeof(ItemPopUp), nameof(ItemPopUp.SetPopUp),
                    new[] { typeof(InventoryCellItemData) });
                if (popup == null || DetailText == null)
                    throw new InvalidOperationException("The Inventory tooltip is unavailable.");

#if ANOMALITH_IL2CPP
                harmony.Patch(popup, postfix: new HarmonyMethod(
                    AccessTools.DeclaredMethod(typeof(StoragePreviewPlugin), nameof(UpdateStorageTooltip))));
#else
                harmony.Patch(popup,
                    postfix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(StoragePreviewPlugin), nameof(PositionIcon))),
                    transpiler: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(StoragePreviewPlugin), nameof(AddCountBeforeLayout))));
#endif
                option = new NativeToggleOption(enabledPreview, Logger, "Inventory Storage Preview", "LABS_STORAGE_PREVIEW");
                Logger.LogInfo("Inventory Storage Preview loaded.");
            }
            catch (Exception error)
            {
                harmony.UnpatchSelf();
                Logger.LogWarning($"Could not attach Inventory Storage Preview. {error.Message}");
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
            foreach (var icon in Icons.Values)
            {
                if (icon != null)
                {
                    icon.enabled = false;
                    Destroy(icon.gameObject);
                }
            }
            Icons.Clear();
        }

#if ANOMALITH_IL2CPP
        private static void UpdateStorageTooltip(ItemPopUp __instance, InventoryCellItemData __0)
        {
            if (!TryAppendCount(__instance, __0))
                return;

            var description = __instance.m_InventoryPopUpItemDeteilText;
            var cell = __0.inventoryItemUI;
            var rect = __instance.m_InventoryPopUpObject?.GetComponent<RectTransform>();
            var canvas = __instance.PopUpCanvas;
            if (cell != null && rect != null && canvas != null && description.gameObject.activeInHierarchy)
            {
                description.ForceMeshUpdate(false, false);
                Canvas.ForceUpdateCanvases();

                var descriptionRect = description.rectTransform;
                var size = rect.sizeDelta;
                size.y = descriptionRect.sizeDelta.y - descriptionRect.anchoredPosition.y + __instance.PopUpTextSpace;
                rect.sizeDelta = size;

                var cellSize = cell.GetSize();
                var scale = cell.GetRectTransform.lossyScale;
                rect.position = cell.transform.position - new Vector3(cellSize.x * scale.x, cellSize.y * scale.y, 0f) * 0.5f;
                var bounds = rect.rect;
                rect.anchoredPosition += new Vector2(bounds.width * 0.5f, -bounds.height * 0.5f);
                __instance.ClampToScreen(rect, canvas);
            }
            PositionIcon(__instance, __0);
        }
#else
        private static IEnumerable<CodeInstruction> AddCountBeforeLayout(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var layout = AccessTools.DeclaredMethod(typeof(Canvas), nameof(Canvas.ForceUpdateCanvases));
            int index = code.FindIndex(instruction => instruction.Calls(layout));
            if (index < 0 || code.FindLastIndex(instruction => instruction.Calls(layout)) != index)
                throw new InvalidOperationException("Expected one layout pass in the Inventory tooltip.");

            // The game has filled the description and will measure and clamp the full tooltip.
            code.InsertRange(index, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(code[index]),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call,
                    AccessTools.DeclaredMethod(typeof(StoragePreviewPlugin), nameof(AppendCount)))
            });
            return code;
        }

        private static void AppendCount(ItemPopUp popup, InventoryCellItemData item) => TryAppendCount(popup, item);
#endif

        private static bool TryAppendCount(ItemPopUp popup, InventoryCellItemData item)
        {
            var description = DetailText.GetValue(popup) as TextMeshProUGUI;
            if (description == null)
                return false;

            Icons.TryGetValue(description, out var icon);
            if (icon != null)
                icon.enabled = false;

            if (!enabledPreview.Value)
                return false;

            var inventory = Inventory.Instance;
            if (item == null || inventory == null || SaveRootGame.Inventory?.Repository == null ||
                string.IsNullOrEmpty(item.GetItemID))
                return false;

            int count = inventory.GetRepositoryItemCount(item.GetItemID);
            string quantity = $"{CountPrefix}{count:000}";
            var sprite = GetStorageSprite();
            if (!description.richText || sprite == null)
            {
                description.text = $"{description.text.TrimEnd()}\n\nIn Storage: {quantity}";
                return true;
            }

            if (icon == null)
            {
                var iconObject = NativeUnity.CreateObject("Storage count icon", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(description.transform, false);
                icon = iconObject.GetComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                Icons[description] = icon;
            }

            icon.sprite = sprite;
            icon.color = new Color32(239, 251, 247, 255);
            icon.enabled = true;
            description.text = $"{description.text.TrimEnd()}\n<size=45%>\n</size>" +
                $"<space=2.65em><color=#EFFBF7><size=125%><b>{quantity}</b></size></color>";
            return true;
        }

        private static Sprite GetStorageSprite()
        {
            var pause = PauseMenu.Instance;
            if (pause == null)
                return null;

            var tab = NativeUnity.Items(pause.GetTabList()).FirstOrDefault(candidate => candidate.tabType == PauseMenu.TabType.Repository);
            var image = tab?.tabIcon != null ? tab.tabIcon.GetComponent<Image>() : null;
            return image != null ? image.overrideSprite : null;
        }

        private static void PositionIcon(ItemPopUp __instance, InventoryCellItemData __0)
        {
            var description = DetailText.GetValue(__instance) as TextMeshProUGUI;
            if (description == null || !Icons.TryGetValue(description, out var icon) || icon == null)
                return;

            if (__0 == null || !description.gameObject.activeInHierarchy)
            {
                icon.enabled = false;
                return;
            }
            if (!icon.enabled)
                return;

            var text = description.textInfo;
            int markerIndex = description.text.LastIndexOf(CountPrefix, StringComparison.Ordinal);
            for (int i = text.characterCount - 1; i >= 0; i--)
            {
                var marker = text.characterInfo[i];
                if (marker.index != markerIndex)
                    continue;

                float bottom = marker.bottomLeft.y;
                float top = marker.topLeft.y;
                for (int j = i + 1; j < text.characterCount; j++)
                {
                    var glyph = text.characterInfo[j];
                    if (!glyph.isVisible)
                        continue;

                    bottom = Mathf.Min(bottom, glyph.bottomLeft.y);
                    top = Mathf.Max(top, glyph.topLeft.y);
                }

                float size = description.fontSize * 1.65f;
                float gap = description.fontSize * 0.4f;
                var rect = icon.rectTransform;
                rect.sizeDelta = new Vector2(size, size);
                rect.localPosition = new Vector3(marker.origin - size * 0.5f - gap, (bottom + top) * 0.5f, 0);
                return;
            }
            icon.enabled = false;
        }
    }
}
