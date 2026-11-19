using System;
using System.Collections.Generic;
using System.Globalization;
using DigitalLogistics.Filtering;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DigitalLogistics.UI.Buses
{
    internal sealed class BusRowControls
    {
        internal const int Width = 360;
        internal const int Padding = 8;
        internal const int RowGap = 8;

        private readonly TreeNodeRender handler;
        private readonly List<Action> refreshers = new();
        private readonly List<Action> lateRefreshers = new();
        internal RectTransform Root { get; }
        internal Action Rebuild { get; }

        internal BusRowControls(RectTransform parent, TreeNodeRender handler, int y, int height, Action rebuild)
        {
            this.handler = handler;
            Rebuild = rebuild;
            Root = new GameObject("BusConfiguration", typeof(RectTransform)).GetComponent<RectTransform>();
            Root.SetParent(parent, false);
            Root.anchorMin = Root.pivot = new Vector2(0, 1);
            Root.anchorMax = Vector2.one;
            Root.anchoredPosition = new Vector2(Padding, -y);
            Root.sizeDelta = new Vector2(-Padding * 2, height);
            var background = Root.gameObject.AddComponent<Image>();
            background.sprite = MachineryStyle.BevelFrame;
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = 0.5f;
            background.color = Color.white;
            background.raycastTarget = false;
        }

        internal void AddRefresh(Action refresh) => refreshers.Add(refresh);

        internal void AddLateRefresh(Action refresh)
        {
            lateRefreshers.Add(refresh);
            refresh();
        }

        internal void Refresh()
        {
            foreach (var refresh in refreshers) refresh();
            foreach (var refresh in lateRefreshers) refresh();
        }

        internal void Destroy()
        {
            refreshers.Clear();
            lateRefreshers.Clear();
            Root.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(Root.gameObject);
        }

        internal void BindEnabled(Func<bool> get, Action<bool> set)
        {
            var root = Root;
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var inputs = root.GetComponentsInChildren<TMP_InputField>(true);
            var dropdowns = root.GetComponentsInChildren<TMP_Dropdown>(true);
            BindToggle(root, new Vector2(Padding, -Padding), get, set, enabled =>
            {
                group.alpha = enabled ? 1 : 0.45f;
                foreach (var input in inputs) input.interactable = enabled;
                foreach (var dropdown in dropdowns) dropdown.interactable = enabled;
            });
        }

        internal MachineryRadioButton BindToggle(RectTransform root, Vector2 position, Func<bool> get, Action<bool> set,
            Action<bool> onRefresh = null, string text = null)
        {
            var toggle = new MachineryRadioButton(text, get());
            toggle.handler.SetParent(handler);
            var rect = toggle.rectTransform;
            rect.SetParent(root, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position + new Vector2(0, -2);
            rect.sizeDelta = new Vector2(toggle.widthPixels, toggle.heightPixels);
            rect.localScale = new Vector3(2, 2, 1);

            void Refresh()
            {
                bool enabled = get();
                toggle.SetOn(enabled);
                onRefresh?.Invoke(enabled);
            }
            toggle.onChanged += value =>
            {
                set(value);
                Refresh();
            };
            refreshers.Add(Refresh);
            Refresh();
            return toggle;
        }

        internal void BindInteger(TMP_InputField input, Func<int> get, Action<int> set)
        {
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            BindText(input, () => get().ToString(CultureInfo.InvariantCulture), () => string.Empty, value =>
            {
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)) set(number);
            });
        }

        internal void BindFilter(TMP_InputField input, Func<string> get, Func<string> defaultValue, Action<string> set)
        {
            BindText(input, get, defaultValue, value =>
            {
                var expression = ParseFilter(string.IsNullOrWhiteSpace(value) ? defaultValue() : value);
                input.textComponent.color = expression == null ? Color.red : Color.white;
                if (expression != null) set(value);
            });
            input.onValueChanged.AddListener(_ => input.textComponent.color = Color.white);
        }

        internal void BindText(TMP_InputField input, Func<string> get, Func<string> defaultValue, Action<string> set)
        {
            input.textComponent.color = Color.white;
            var placeholder = (TMP_Text)input.placeholder;
            placeholder.color = Color.gray;
            placeholder.fontStyle = FontStyles.Normal;
            input.onSelect.AddListener(_ => placeholder.text = string.Empty);
            input.onDeselect.AddListener(_ => placeholder.text = defaultValue());
            input.onEndEdit.AddListener(value => set(value));
            void Refresh()
            {
                if (input.isFocused) return;
                input.SetTextWithoutNotify(get());
                placeholder.text = defaultValue();
            }
            refreshers.Add(Refresh);
            Refresh();
        }

        internal static CachedItemExpression ParseFilter(string text)
        {
            try { return CachedItemExpressionParser.Parse(text); }
            catch (Superpower.ParseException) { return null; }
        }
    }
}
