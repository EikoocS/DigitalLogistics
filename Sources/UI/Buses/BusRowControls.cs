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
        internal Action Rebuild { get; }

        internal BusRowControls(RectTransform parent, TreeNodeRender handler, int y, int height, Action rebuild)
        {
            this.handler = handler;
            Rebuild = rebuild;
            Root = CreateSection(parent, "BusConfiguration", y, height);
        }

        internal static RectTransform CreateStrip(RectTransform parent, string name, Vector2 position,
            int width, int height, bool right = false)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.pivot = new Vector2(right ? 1 : 0, 1);
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        internal static RectTransform CreateSection(RectTransform parent, string name, int y, int height)
        {
            var rect = CreateStrip(parent, name, new Vector2(Padding, -y), -Padding * 2, height);
            var background = rect.gameObject.AddComponent<Image>();
            background.sprite = MachineryStyle.BevelFrame;
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = 0.5f;
            background.color = Color.white;
            background.raycastTarget = false;
            return rect;
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

        internal void BindAvailability(RectTransform parent, Func<bool> enabled)
        {
            var inputs = parent.GetComponentsInChildren<TMP_InputField>(true);
            var dropdowns = parent.GetComponentsInChildren<TMP_Dropdown>(true);
            AddLateRefresh(() =>
            {
                bool available = enabled();
                foreach (var input in inputs) input.interactable = available;
                foreach (var dropdown in dropdowns) dropdown.interactable = available;
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

        internal void BindFilter(TMP_InputField input, Func<string> get, Func<string> defaultValue, Action<string> set,
            bool inheritWhenEmpty = false) =>
            AddRefresh(MachineryInputBinding.BindFilter(input, get, defaultValue, set, inheritWhenEmpty));

        internal void BindText(TMP_InputField input, Func<string> get, Func<string> defaultValue, Action<string> set) =>
            AddRefresh(MachineryInputBinding.BindText(input, get, defaultValue, set));
    }
}
