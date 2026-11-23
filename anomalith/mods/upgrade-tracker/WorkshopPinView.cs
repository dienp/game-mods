using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalithUpgradeTracker
{
    internal sealed class WorkshopPinView
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly NativeField Materials = NativeField.Find(typeof(DevMenuBase), "m_RequiredMaterials", Fields);
        private static readonly NativeField Info = NativeField.Find(typeof(DevMenuBase), "m_Info", Fields);
        private static readonly NativeField MaterialRows = NativeField.Find(typeof(RequiredMaterials), "m_NeedMatTextInsList", Fields);
        private static readonly NativeField LeftMaterialColumn = NativeField.Find(typeof(RequiredMaterials), "m_NeedMatTextsRootL", Fields);
        private static readonly NativeField ButtonBackground = NativeField.Find(typeof(DevelopmetInfo), "m_DevButtonBG", Fields);
        private static readonly NativeField ButtonText = NativeField.Find(typeof(DevelopmetInfo), "m_DevButtonText", Fields);

        private readonly DevMenuBase menu;
        private readonly RequiredMaterials materials;
        private readonly RectTransform materialsPanel;
        private readonly RectTransform parent;
        private readonly RectTransform rect;
        private readonly Button button;
        private readonly TextMeshProUGUI action;
        private readonly TextMeshProUGUI shortcut;

        private WorkshopPinView(DevMenuBase menu, RectTransform parent, GameObject instance,
            RequiredMaterials materials, RectTransform materialsPanel, Image background, TextMeshProUGUI font, Action onToggle)
        {
            this.menu = menu;
            this.parent = parent;
            this.materials = materials;
            this.materialsPanel = materialsPanel;
            rect = instance.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = parent.pivot;
            rect.pivot = new Vector2(1, 0);

            var target = rect.GetComponent<Image>();
            if (background != null)
            {
                target.sprite = background.sprite;
                target.type = background.type == Image.Type.Sliced ? Image.Type.Sliced : Image.Type.Simple;
            }

            button = rect.GetComponent<Button>();
            button.targetGraphic = target;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
#if ANOMALITH_IL2CPP
            button.onClick.AddListener(onToggle);
#else
            button.onClick.AddListener(() => onToggle());
#endif
            action = CreateText("Pin action", font, 21, TextAlignmentOptions.MidlineLeft);
            action.color = new Color(0.92f, 0.98f, 0.94f);
            shortcut = CreateText("Pin shortcut", font, 17, TextAlignmentOptions.MidlineRight);
            shortcut.color = new Color(0.78f, 0.88f, 0.84f);
        }

        public static WorkshopPinView TryCreate(DevMenuBase menu, Action onToggle)
        {
            var materials = Materials?.GetValue(menu) as RequiredMaterials;
            var info = Info?.GetValue(menu) as DevelopmetInfo;
            var parent = NativeUnity.Cast<RectTransform>(menu.transform);
            if (materials == null || info == null || parent == null)
                return null;
            var column = LeftMaterialColumn?.GetValue(materials) as RectTransform;
            if (column == null || !(NativeUnity.Cast<RectTransform>(column.parent) is RectTransform materialsPanel))
                return null;
            var background = ButtonBackground?.GetValue(info) as Image;
            var font = ButtonText?.GetValue(info) as TextMeshProUGUI;
            return font == null || font.font == null ? null :
                NativeUnity.CreateInactive("Workshop pin toggle",
                    instance => new WorkshopPinView(menu, parent, instance, materials, materialsPanel, background, font, onToggle),
                    typeof(RectTransform), typeof(Image), typeof(Button));
        }

        public bool IsFor(DevMenuBase candidate) => rect != null && menu == candidate;

        public void Show(bool pinned, string key)
        {
            if (rect == null || parent == null || materials == null || materialsPanel == null ||
                !materials.gameObject.activeInHierarchy)
            {
                Hide();
                return;
            }

            var rows = NativeUnity.FieldItems<GameObject>(MaterialRows?.GetValue(materials));
            float top = float.NegativeInfinity;
            foreach (var row in rows)
            {
                if (row == null || !row.activeInHierarchy ||
                    !(NativeUnity.Cast<RectTransform>(row.transform) is RectTransform rowRect))
                    continue;
                var corner = rowRect.TransformPoint((Vector3)rowRect.rect.max);
                top = Mathf.Max(top, parent.InverseTransformPoint(corner).y);
            }
            if (float.IsNegativeInfinity(top))
            {
                Hide();
                return;
            }

            string label = pinned ? "Unpin recipe" : "Pin recipe";
            key = key ?? "";
            if (action.text != label || shortcut.text != key)
            {
                action.text = label;
                shortcut.text = key;
                float keyWidth = Mathf.Min(100, Mathf.Ceil(shortcut.GetPreferredValues().x));
                float actionWidth = Mathf.Ceil(action.GetPreferredValues().x);
                rect.sizeDelta = new Vector2(Mathf.Max(158, actionWidth + keyWidth + 42), 34);
                action.rectTransform.offsetMax = new Vector2(-keyWidth - 26, 0);
                shortcut.rectTransform.offsetMin = new Vector2(rect.sizeDelta.x - keyWidth - 13, 0);
                button.colors = new ColorBlock
                {
                    normalColor = pinned ? new Color(0.19f, 0.27f, 0.26f) : new Color(0.11f, 0.18f, 0.19f),
                    highlightedColor = new Color(0.23f, 0.33f, 0.31f),
                    pressedColor = new Color(0.11f, 0.18f, 0.18f),
                    selectedColor = new Color(0.20f, 0.30f, 0.28f),
                    disabledColor = new Color(0.14f, 0.20f, 0.20f, 0.5f),
                    colorMultiplier = 1,
                    fadeDuration = 0.1f
                };
            }
            float right = parent.InverseTransformPoint(materialsPanel.TransformPoint(materialsPanel.rect.max)).x;
            rect.anchoredPosition = new Vector2(right, top + 6);
            if (rect.GetSiblingIndex() != parent.childCount - 1)
                rect.SetAsLastSibling();
            rect.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (rect != null)
                rect.gameObject.SetActive(false);
        }

        public void Destroy()
        {
            if (rect != null)
            {
                Hide();
                UnityEngine.Object.Destroy(rect.gameObject);
            }
        }

        private TextMeshProUGUI CreateText(string name, TextMeshProUGUI font, float size, TextAlignmentOptions alignment)
        {
            var text = NativeUnity.CreateObject(name, typeof(RectTransform), typeof(TextMeshProUGUI))
                .GetComponent<TextMeshProUGUI>();
            text.rectTransform.SetParent(rect, false);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(12, 0);
            text.rectTransform.offsetMax = new Vector2(-12, 0);
            text.font = font.font;
            text.fontSharedMaterial = font.fontSharedMaterial;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = font.color;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            text.richText = false;
            return text;
        }
    }
}
