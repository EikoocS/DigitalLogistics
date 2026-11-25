using System;
using System.Collections.Generic;
using System.Globalization;
using DigitalLogistics.UI.Common;
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
        internal const int Line = 44;
        internal const int NameX = Padding + 48;

        private readonly TreeNodeRender handler;
        private readonly List<Action> refreshers = new();
        private readonly List<Action> lateRefreshers = new();
        internal RectTransform Root { get; }
        internal BusRowControls(RectTransform parent, TreeNodeRender handler, int y, int height)
        {
            this.handler = handler;
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

        internal void BindFilter(TMP_InputField input, Func<string> get, Func<string> defaultValue, Action<string> set) =>
            AddRefresh(MachineryInputBinding.BindFilter(input, get, defaultValue, set));

        internal void BindText(TMP_InputField input, Func<string> get, Func<string> defaultValue, Action<string> set) =>
            AddRefresh(MachineryInputBinding.BindText(input, get, defaultValue, set));
    }
}
