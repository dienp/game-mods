using System.Linq;
using System;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Anomalith
{
    internal sealed class NativeToggleOption : IDisposable
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        // Use the same native ON/OFF prefab and local parameters as the overlay settings.
        private static readonly NativeField Prefab = NativeField.Find(typeof(OptionItemHolder), "m_SelectPrefab", Private);
        private static readonly NativeField Content = NativeField.Find(typeof(OptionItemHolder), "m_OptionContentRoot", Private);
        private static readonly NativeField Current = NativeField.Find(typeof(OptionItemHolder), "m_CurrentOptionItem", Private);
        private static readonly NativeField IsOpen = NativeField.Find(typeof(OptionItemHolder), "m_IsOpen", Private);
        private static readonly NativeField Parameter = NativeField.Find(typeof(OptionItemBase), "m_OptionParam", Private);
        private static readonly NativeField ItemName = NativeField.Find(typeof(OptionItemBase), "m_ItemNameText", Private);

        private readonly ConfigEntry<bool> enabled;
        private readonly ManualLogSource logger;
        private readonly string label;
        private readonly string id;
        private OptionMenu menu;
        private OptionItemHolder holder;
        private OptionItemSelect item;
        private TextMeshProUGUI name;
        private OptionParam param;
        private int displayedValue;
        private bool failed;

        internal NativeToggleOption(ConfigEntry<bool> enabled, ManualLogSource logger, string label, string id)
        {
            this.enabled = enabled;
            this.logger = logger;
            this.label = label;
            this.id = id;
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
                if (menu != currentMenu || item == null || holder == null || !holder.GetOptionList().Contains(item))
                {
                    Dispose();
                    if (!Attach(currentMenu))
                        return;
                }

                if (name.text != label)
                    name.text = label;

                if (param.m_Value != displayedValue)
                {
                    displayedValue = param.m_Value;
                    enabled.Value = displayedValue == 0;
                    logger.LogInfo($"{label} set to {(enabled.Value ? "ON" : "OFF")}.");
                }
                else if (enabled.Value != (displayedValue == 0))
                {
                    displayedValue = enabled.Value ? 0 : 1;
                    param.m_Value = displayedValue;
                    item.UIUpdate();
                }
            }
            catch (Exception error)
            {
                failed = true;
                Dispose();
                logger.LogWarning($"Could not add the {label} setting. {error}");
            }
        }

        private bool Attach(OptionMenu currentMenu)
        {
            holder = NativeUnity.Items(currentMenu.GetOptionHolderList()).FirstOrDefault(candidate =>
                candidate != null && candidate.m_HolderGroupType == OptionGroupType.OTHERS);
            if (holder == null || holder.GetOptionList().Count == 0)
                return false;

            var prefab = Prefab?.GetValue(holder) as OptionItemSelect;
            var content = Content?.GetValue(holder) as RectTransform;
            if (prefab == null || content == null || Parameter == null || ItemName == null ||
                Current == null || IsOpen == null)
                throw new InvalidOperationException("The native Options selector is unavailable.");

            menu = currentMenu;
            var list = holder.GetOptionList();
            item = Object.Instantiate(prefab, content);
            name = (TextMeshProUGUI)ItemName.GetValue(item);
            displayedValue = enabled.Value ? 0 : 1;
            param = new OptionParam
            {
                m_Id = id,
                m_OptionGroupType = OptionGroupType.OTHERS,
                m_OptionParamType = OptionParamType.SELECT,
                m_OptionSelectType = OptionSelectType.ON_OFF,
                m_MaxValue = 1,
                m_Value = displayedValue
            };

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
            holder.MaskUpdate();
            logger.LogInfo($"Added the {label} toggle to Other Settings.");
            return true;
        }

        public void Dispose()
        {
            if (item != null)
            {
                if (holder != null)
                {
                    var list = holder.GetOptionList();
                    int index = list.IndexOf(item);
                    if (index >= 0)
                    {
                        list.RemoveAt(index);
                        for (int i = index; i < list.Count; i++)
                        {
                            if (list[i] != null)
                                list[i].ObjectNumber = i;
                        }

                        if ((Current.GetValue(holder) as OptionItemBase) == item)
                        {
                            var replacement = NativeUnity.Items(list).LastOrDefault(row => row != null && row.gameObject.activeSelf);
                            Current.SetValue(holder, replacement);
                            if (replacement != null && (bool)IsOpen.GetValue(holder))
                            {
                                replacement.UseSelect();
                                if (menu != null)
                                    menu.GuideUpdate(replacement.GetOptionParam());
                            }
                        }
                        holder.MaskUpdate();
                    }
                }

                item.gameObject.SetActive(false);
                Object.Destroy(item.gameObject);
            }
            item = null;
            holder = null;
            menu = null;
        }
    }
}
