using System;
using DigitalLogistics.Filtering;
using TMPro;
using UnityEngine;

namespace DigitalLogistics.UI.Common
{
    internal static class MachineryInputBinding
    {
        internal static Action BindFilter(TMP_InputField input, Func<string> get, Func<string> defaultValue,
            Action<string> set, bool inheritWhenEmpty = false)
        {
            var refresh = BindText(input, get, defaultValue, value =>
            {
                value = value.Trim();
                bool valid = value.Length == 0
                    ? inheritWhenEmpty || IsValidFilter(defaultValue())
                    : IsValidFilter(value);
                input.textComponent.color = valid ? Color.white : Color.red;
                if (valid) set(value);
            });
            input.onValueChanged.AddListener(_ => input.textComponent.color = Color.white);
            return refresh;
        }

        internal static Action BindText(TMP_InputField input, Func<string> get, Func<string> defaultValue,
            Action<string> set)
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
            Refresh();
            return Refresh;
        }

        private static bool IsValidFilter(string text)
        {
            try { return CachedItemExpressionParser.Parse(text) != null; }
            catch (Superpower.ParseException) { return false; }
        }
    }
}
