using System.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
#if !ANOMALITH_IL2CPP
using BepInEx.Bootstrap;
#endif
using BepInEx.Configuration;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalithMinimap
{
    internal sealed class OverlayOptions : IDisposable
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
#if !ANOMALITH_IL2CPP
        private const string TrackerId = "labs.anomalith.upgradetracker";
#endif
        private const string SizeLabel = "Minimap Size";

        private static readonly NativeField Prefab = NativeField.Find(typeof(OptionItemHolder), "m_SelectPrefab", Private);
        private static readonly NativeField SliderPrefab = NativeField.Find(typeof(OptionItemHolder), "m_SliderPrefab", Private);
        private static readonly NativeField SliderValueText = NativeField.Find(typeof(OptionItemSlider), "m_ValueText", Private);
        private static readonly NativeField Content = NativeField.Find(typeof(OptionItemHolder), "m_OptionContentRoot", Private);
        private static readonly NativeField Current = NativeField.Find(typeof(OptionItemHolder), "m_CurrentOptionItem", Private);
        private static readonly NativeField IsOpen = NativeField.Find(typeof(OptionItemHolder), "m_IsOpen", Private);
        private static readonly NativeField Parameter = NativeField.Find(typeof(OptionItemBase), "m_OptionParam", Private);
        private static readonly NativeField ItemName = NativeField.Find(typeof(OptionItemBase), "m_ItemNameText", Private);

        private readonly ConfigEntry<bool> showMap;
        private readonly ConfigEntry<int> mapSize;
        private readonly ManualLogSource logger;
        private readonly List<Row> rows = new List<Row>(2);
        private OptionMenu menu;
        private OptionItemHolder holder;
        private OptionItemSlider sizeSlider;
        private TextMeshProUGUI sizeName;
        private TextMeshProUGUI sizeValue;
        private int displayedSize;
        private string sizeText;
        private bool failed;

        internal OverlayOptions(ConfigEntry<bool> showMap, ConfigEntry<int> mapSize, ManualLogSource logger)
        {
            this.showMap = showMap;
            this.mapSize = mapSize;
            this.logger = logger;
        }

        internal void Refresh()
        {
            if (failed || DialogManager.Instance == null || !DialogManager.IsOpenDialog)
                return;
            var currentMenu = NativeUnity.Cast<OptionMenu>(DialogManager.GetTopDialog());
            if (currentMenu == null || !currentMenu.isActiveAndEnabled)
                return;

            try
            {
                if (menu != currentMenu || rows.Count == 0 || rows[0].Item == null ||
                    sizeSlider == null || holder == null || !holder.GetOptionList().Contains(rows[0].Item) ||
                    !holder.GetOptionList().Contains(sizeSlider))
                {
                    Dispose();
                    menu = currentMenu;
                    if (!AttachRows())
                        return;
                }

                foreach (var row in rows)
                    row.Sync();

                if (sizeName.text != SizeLabel)
                    sizeName.text = SizeLabel;
                var sizeParam = sizeSlider.GetOptionParam();
                if (sizeParam.m_Value != displayedSize)
                {
                    mapSize.Value = sizeParam.m_Value;
                    UpdateDisplayedSize();
                    sizeParam.m_Value = displayedSize;
                }
                else if (mapSize.Value != displayedSize)
                {
                    UpdateDisplayedSize();
                    sizeParam.m_Value = displayedSize;
                    sizeSlider.UIUpdate();
                }
                if (sizeValue.text != sizeText)
                    sizeValue.text = sizeText;
            }
            catch (Exception error)
            {
                failed = true;
                Dispose();
                logger.LogWarning($"Could not add the overlay settings. {error}");
            }
        }

        private bool AttachRows()
        {
            foreach (var candidate in menu.GetOptionHolderList())
            {
                if (candidate != null && candidate.m_HolderGroupType == OptionGroupType.OTHERS)
                {
                    holder = candidate;
                    break;
                }
            }
            if (holder == null || holder.GetOptionList().Count == 0)
                return false;

            var prefab = Prefab?.GetValue(holder) as OptionItemSelect;
            var audioHolder = NativeUnity.Items(menu.GetOptionHolderList()).FirstOrDefault(candidate =>
                candidate != null && candidate.m_HolderGroupType == OptionGroupType.SOUND);
            var sliderPrefab = SliderPrefab?.GetValue(audioHolder ?? holder) as OptionItemSlider;
            var content = Content?.GetValue(holder) as RectTransform;
            if (prefab == null || content == null || Parameter == null || ItemName == null ||
                Current == null || IsOpen == null || sliderPrefab == null || SliderValueText == null)
                throw new InvalidOperationException("The native Options selector or slider is unavailable.");

            AddRow(prefab, content, "Minimap", "LABS_MINIMAP_VISIBILITY", showMap);
            sizeSlider = Object.Instantiate(sliderPrefab, content);
            sizeValue = (TextMeshProUGUI)SliderValueText.GetValue(sizeSlider);
            UpdateDisplayedSize();
            sizeName = InitializeRow(sizeSlider, new OptionParam
            {
                m_Id = "LABS_MINIMAP_SIZE",
                m_OptionGroupType = OptionGroupType.OTHERS,
                m_OptionParamType = OptionParamType.SLIDER,
                m_MinValue = MinimapPlugin.MinimumSize,
                m_DefaultValue = MinimapPlugin.DefaultSize,
                m_MaxValue = MinimapPlugin.MaximumSize,
                m_Value = displayedSize
            }, SizeLabel);
            sizeValue.text = sizeText;
#if !ANOMALITH_IL2CPP
            if (Chainloader.PluginInfos.TryGetValue(TrackerId, out var trackerInfo) &&
                trackerInfo.Instance != null &&
                trackerInfo.Instance.Config.TryGetEntry("Tracker", "ShowPinnedItem", out ConfigEntry<bool> showPin))
            {
                AddRow(prefab, content, "Pinned Item", "LABS_PINNED_ITEM_VISIBILITY", showPin);
            }
#endif
            holder.MaskUpdate();
            logger.LogInfo($"Added {rows.Count} native overlay toggles and the Minimap Size slider to Other Settings.");
            return true;
        }

        private void UpdateDisplayedSize()
        {
            displayedSize = mapSize.Value;
            sizeText = $"{displayedSize / 10f:0.0}×";
        }

        private void AddRow(OptionItemSelect prefab, RectTransform content, string label, string id,
            ConfigEntry<bool> setting)
        {
            var item = Object.Instantiate(prefab, content);
            var name = (TextMeshProUGUI)ItemName.GetValue(item);
            var row = new Row(item, setting, name, label);
            rows.Add(row);

            // Native ON_OFF selectors use 0 for ON and 1 for OFF.
            var param = new OptionParam
            {
                m_Id = id,
                m_OptionGroupType = OptionGroupType.OTHERS,
                m_OptionParamType = OptionParamType.SELECT,
                m_OptionSelectType = OptionSelectType.ON_OFF,
                m_MaxValue = 1,
                m_Value = row.Value
            };
            InitializeRow(item, param, label);
        }

        private TextMeshProUGUI InitializeRow(OptionItemBase item, OptionParam param, string label)
        {
            var list = holder.GetOptionList();
            var name = (TextMeshProUGUI)ItemName.GetValue(item);
            // Keep the parameter local to this row. The mod label has no game string-table entry.
            Parameter.SetValue(item, param);
            foreach (var component in name.GetComponents<Behaviour>())
            {
                if (NativeUnity.IsLocalized(component))
                {
                    component.enabled = false;
                    Object.Destroy(component);
                }
            }
            name.text = label;
            item.SetHolder(holder);
            item.ObjectNumber = list.Count;
            item.IsInputActive = (bool)IsOpen.GetValue(holder);
            item.UIUpdate();
            list.Add(item);
            return name;
        }

        public void Dispose()
        {
            if (holder != null)
            {
                var list = holder.GetOptionList();
                var selected = Current?.GetValue(holder) as OptionItemBase;
                bool restoreSelection = false;
                var items = new List<OptionItemBase>();
                foreach (var row in rows)
                    items.Add(row.Item);
                if (sizeSlider != null)
                    items.Add(sizeSlider);
                foreach (var item in items)
                {
                    list.Remove(item);
                    restoreSelection |= selected == item;
                    if (item != null)
                    {
                        item.gameObject.SetActive(false);
                        Object.Destroy(item.gameObject);
                    }
                }
                for (int i = 0; i < list.Count; i++)
                    if (list[i] != null)
                        list[i].ObjectNumber = i;
                if (restoreSelection)
                {
                    var replacement = NativeUnity.Items(list).LastOrDefault(item => item != null && item.gameObject.activeSelf);
                    Current.SetValue(holder, replacement);
                    if (replacement != null && (bool)IsOpen.GetValue(holder))
                    {
                        replacement.UseSelect();
                        if (menu != null)
                            menu.GuideUpdate(replacement.GetOptionParam());
                    }
                }
                if (items.Count > 0)
                    holder.MaskUpdate();
            }
            rows.Clear();
            sizeSlider = null;
            sizeName = null;
            sizeValue = null;
            holder = null;
            menu = null;
        }

        private sealed class Row
        {
            private readonly ConfigEntry<bool> setting;
            private readonly TextMeshProUGUI name;
            private readonly string label;
            internal OptionItemSelect Item { get; }
            internal int Value { get; private set; }

            internal Row(OptionItemSelect item, ConfigEntry<bool> setting, TextMeshProUGUI name, string label)
            {
                Item = item;
                this.setting = setting;
                this.name = name;
                this.label = label;
                Value = setting.Value ? 0 : 1;
            }

            internal void Sync()
            {
                if (name.text != label)
                    name.text = label;
                var param = Item.GetOptionParam();
                if (param.m_Value != Value)
                {
                    Value = param.m_Value;
                    setting.Value = Value == 0;
                }
                else if (setting.Value != (Value == 0))
                {
                    Value = setting.Value ? 0 : 1;
                    param.m_Value = Value;
                    Item.UIUpdate();
                }
            }
        }
    }
}
