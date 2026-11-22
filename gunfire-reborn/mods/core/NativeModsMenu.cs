using System.Globalization;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UIScript;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GunfireReborn.Mods.Core;

internal sealed class NativeModsMenu
{
    private const string ContentName = "Content_ModsCore";
    private readonly ManualLogSource log;
    private readonly List<Setting> settings = new();
    private readonly HashSet<ConfigEntryBase> registered = new();
    private PCSettingPanel_logic? pendingPanel;

    internal static NativeModsMenu? Active { get; set; }

    internal NativeModsMenu(ManualLogSource log) => this.log = log;

    internal void Add(ConfigEntryBase entry, string label, string group)
    {
        if (registered.Add(entry))
        {
            settings.Add(new Setting(entry, label, group));
            log.LogInfo($"Registered Mods > {group} > {label}.");
        }
    }

    internal void Install()
    {
        try
        {
            var harmony = new Harmony(Plugin.Id);
            harmony.Patch(AccessTools.Method(typeof(PCSettingPanel_logic), nameof(PCSettingPanel_logic.Update)),
                postfix: new HarmonyMethod(typeof(NativeModsMenu), nameof(AttachToPanel)));
            harmony.Patch(AccessTools.Method(typeof(PCSettingPanel_logic), nameof(PCSettingPanel_logic.OnEnable)),
                postfix: new HarmonyMethod(typeof(NativeModsMenu), nameof(QueuePanel)));
        }
        catch (Exception error)
        {
            log.LogWarning($"Could not hook the Mods tab. Settings remain in the config files. {error.GetBaseException().Message}");
        }
    }

    private static void QueuePanel(PCSettingPanel_logic __instance)
    {
        // The native tab group registers its children after the panel's OnEnable.
        if (Active is { } menu)
            menu.pendingPanel = __instance;
    }

    private static void AttachToPanel(PCSettingPanel_logic __instance)
    {
        if (Active is not { pendingPanel: { } pending } menu || pending != __instance)
            return;
        menu.pendingPanel = null;
        menu.Attach(__instance);
    }

    private void Attach(PCSettingPanel_logic panel)
    {
        if (settings.Count == 0)
            return;

        RectTransform? content = null;
        GameObject? tab = null;
        HorizontalLayoutGroup? layout = null;
        float previousSpacing = 0;
        int tabId = 0;
        bool contentRegistered = false;
        try
        {
            var scroll = panel.m_U3DScroll;
            var viewport = scroll.viewport;
            if (viewport.Find(ContentName))
                return;

            var tabStrip = panel.transform.Find("bg_windows/tab_switch");
            var tabGroup = tabStrip.GetComponent<M1ToggleGroup>();
            var tabTemplate = tabStrip.Find("4").GetComponent<RectTransform>();
            layout = tabStrip.GetComponent<HorizontalLayoutGroup>();
            previousSpacing = layout.spacing;

            var rowTemplates = viewport.Find("Content_2");
            var sliderTemplate = rowTemplates.Find("settion_group_1/201");
            var toggleTemplate = rowTemplates.Find("settion_group_1/207");
            var headerTemplate = rowTemplates.Find("settion_group_2/title");

            float rowWidth = toggleTemplate.GetComponent<RectTransform>().rect.width;
            content = CreateContent(viewport, scroll.content, rowWidth);
            var refreshRows = new List<Action>();
            foreach (var group in settings.GroupBy(setting => setting.Group))
            {
                AddHeader(headerTemplate, content, group.Key);
                foreach (var setting in group)
                    refreshRows.Add(AddRow(panel, content, setting, toggleTemplate, sliderTemplate));
            }

            foreach (var entry in panel.m_ContentDict)
                tabId = Math.Max(tabId, entry.Key);
            tabId++;
            tab = Object.Instantiate(tabTemplate.gameObject, content, false);
            tab.SetActive(false);
            tab.name = tabId.ToString(CultureInfo.InvariantCulture);
            var tabRect = tab.GetComponent<RectTransform>();
            var tabToggle = tab.GetComponent<M1Toggle>();
            Remove<SpecialMultiLanguage>(tab);
            foreach (var label in tab.GetComponentsInChildren<TextMeshProUGUI>(true))
                label.text = "Mods";

            float width = tabRect.sizeDelta.x / 2f;
            tabRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            tabToggle.onClick = new UnityEngine.Events.UnityEvent();
            tabToggle.onValueChanged = new M1Toggle.ToggleEvent();
            tabToggle.onValueChanged.AddListener((Action<bool>)(selected =>
            {
                panel.OnTabSelected(tabId, selected);
                if (selected)
                {
                    panel.m_Desc.text = string.Empty;
                    foreach (var refresh in refreshRows)
                        refresh();
                    LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                }
            }));
            tabToggle.group = null;
            tabToggle.SetIsOnWithoutNotify(false);

            int nativeTabCount = tabGroup.m_Toggles.Count;
            if (nativeTabCount < 2)
                throw new InvalidOperationException("The native settings tab group has too few tabs.");
            float spacing = (previousSpacing * (nativeTabCount - 1) - width) / nativeTabCount;
            if (spacing < 0)
                throw new InvalidOperationException("There is no room in the settings tab strip.");

            panel.m_ContentDict.Add(tabId, content);
            contentRegistered = true;
            tabRect.SetParent(tabStrip, false);
            tabToggle.group = tabGroup;
            layout.spacing = spacing;
            tab.SetActive(true);
            LayoutRebuilder.MarkLayoutForRebuild(tabStrip.GetComponent<RectTransform>());
            log.LogInfo("Attached the Mods tab to native Settings.");
        }
        catch (Exception error)
        {
            if (tab)
            {
                tab!.SetActive(false);
                Object.Destroy(tab);
            }
            if (contentRegistered)
                panel.m_ContentDict.Remove(tabId);
            if (layout)
                layout!.spacing = previousSpacing;
            if (content)
                Object.Destroy(content!.gameObject);
            log.LogWarning($"Could not add the Mods tab. Settings remain in the config files. {error.GetBaseException().Message}");
        }
    }

    private static RectTransform CreateContent(RectTransform viewport, RectTransform nativeContent, float rowWidth)
    {
        var root = new GameObject(ContentName);
        root.SetActive(false);
        var rect = root.AddComponent<RectTransform>();
        rect.SetParent(viewport, false);
        rect.anchorMin = nativeContent.anchorMin;
        rect.anchorMax = nativeContent.anchorMax;
        rect.pivot = nativeContent.pivot;
        rect.anchoredPosition = nativeContent.anchoredPosition;
        rect.sizeDelta = new Vector2(nativeContent.sizeDelta.x, 0);

        var layout = root.AddComponent<VerticalLayoutGroup>();
        int rightPadding = Mathf.Max(0, Mathf.RoundToInt(rect.rect.width - rowWidth));
        layout.padding = new RectOffset(0, rightPadding, 8, 8);
        layout.spacing = 8;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var height = root.AddComponent<ContentSizeFitter>();
        height.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rect;
    }

    private static void AddHeader(Transform template, Transform parent, string title)
    {
        var header = Object.Instantiate(template.gameObject, parent, false);
        header.name = "Mod group";
        Remove<SpecialMultiLanguage>(header);
        Object.DestroyImmediate(header.GetComponent<M1ContentSizeFitter>());
        var label = header.GetComponentInChildren<TextMeshProUGUI>(true);
        label.text = title;
        var layout = header.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(layout.padding.left, layout.padding.right, 3, 0);
        var height = header.AddComponent<LayoutElement>();
        height.preferredHeight = 42;
        header.SetActive(true);
    }

    private static Action AddRow(PCSettingPanel_logic panel, Transform parent, Setting setting,
        Transform toggleTemplate, Transform sliderTemplate)
    {
        bool isToggle = setting.Entry.SettingType == typeof(bool);
        var template = isToggle ? toggleTemplate : sliderTemplate;
        var row = Object.Instantiate(template.gameObject, parent, false);
        row.name = setting.Label;

        // Keep the visuals and input controls, and detach the bindings to the original game setting.
        Remove<PCSettingItem_Logic>(row);
        Remove<PCSettingClickBox_logic>(row);
        Remove<PCSettingSlider_logic>(row);
        Remove<global::UI.CAutoSlider>(row);
        Remove<M1ControllerSetting>(row);
        Remove<M1ControllerSettingSlider>(row);
        Remove<DYControl.DYScrollSelect>(row);

        var title = row.transform.Find("Title").GetComponent<TextMeshProUGUI>();
        title.text = setting.Label;
        var layout = row.AddComponent<LayoutElement>();
        layout.preferredHeight = template.GetComponent<RectTransform>().sizeDelta.y;

        var button = row.GetComponent<M1Button>();
        Action<Transform> showHelp = _ => panel.m_Desc.text = setting.Entry.Description.Description;
        Action<Transform> clearHelp = _ =>
        {
            if (panel.m_Desc.text == setting.Entry.Description.Description)
                panel.m_Desc.text = string.Empty;
        };
        button.onEnter = showHelp;
        button.onExit = clearHelp;
        button.oFunconselect = showHelp;
        button.oFunconDeSelect = clearHelp;
        button.onClick = new Button.ButtonClickedEvent();

        var refresh = isToggle ? BindToggle(row, (ConfigEntry<bool>)setting.Entry) : BindSlider(row, setting.Entry);
        row.SetActive(true);
        return refresh;
    }

    private static Action BindToggle(GameObject row, ConfigEntry<bool> entry)
    {
        var options = row.transform.Find("Item/ClickGroup");
        var on = options.Find("2").GetComponent<Toggle>();
        var off = options.Find("1").GetComponent<Toggle>();
        SetOption(on, true);
        SetOption(off, false);
        return () =>
        {
            var selected = entry.Value ? on : off;
            selected.Set(true, false);
        };

        void SetOption(Toggle option, bool value)
        {
            Remove<EventTrigger>(option.gameObject);
            option.onValueChanged = new Toggle.ToggleEvent();
            option.onValueChanged.AddListener((Action<bool>)(selected =>
            {
                if (selected)
                    entry.Value = value;
            }));
        }
    }

    private static Action BindSlider(GameObject row, ConfigEntryBase entry)
    {
        var slider = row.GetComponentInChildren<M1Slider>(true);
        var text = row.transform.Find("Item/Slider_PCunit/Num").GetComponent<TextMeshProUGUI>();
        var range = entry.Description.AcceptableValues;
        bool isInteger = entry is ConfigEntry<int>;
        if (range is AcceptableValueRange<int> intRange)
        {
            slider.minValue = intRange.MinValue;
            slider.maxValue = intRange.MaxValue;
        }
        else if (range is AcceptableValueRange<float> floatRange)
        {
            slider.minValue = floatRange.MinValue;
            slider.maxValue = floatRange.MaxValue;
        }

        slider.wholeNumbers = isInteger;
        slider.DragStart = new Slider.SliderEvent();
        slider.DragStop = new Slider.SliderEvent();
        slider.PointerDown = new Slider.SliderEvent();
        slider.PointerUp = new Slider.SliderEvent();
        slider.onValueChanged = new Slider.SliderEvent();
        slider.onValueChanged.AddListener((Action<float>)(value =>
        {
            entry.BoxedValue = isInteger ? (object)Mathf.RoundToInt(value) : value;
            SetReadout();
        }));

        return () =>
        {
            slider.Set(Convert.ToSingle(entry.BoxedValue, CultureInfo.InvariantCulture), false);
            SetReadout();
        };

        void SetReadout()
        {
            text.text = Convert.ToSingle(entry.BoxedValue, CultureInfo.InvariantCulture)
                .ToString(isInteger ? "0" : "0.##", CultureInfo.CurrentCulture);
        }
    }

    private static void Remove<T>(GameObject root) where T : Component
    {
        foreach (var component in root.GetComponentsInChildren<T>(true))
            Object.DestroyImmediate(component);
    }

    private sealed record Setting(ConfigEntryBase Entry, string Label, string Group);
}
