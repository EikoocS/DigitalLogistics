using System;
using TMPro;
using UnityEngine;

namespace DigitalLogistics.UI.Buses
{
    internal static class UnlimitedTransferControls
    {
        internal static void Bind(BusRowControls controls, RectTransform parent, Vector2 position,
            Func<bool> get, Action<bool> set, Func<bool> enabled, Transform amountRoot, string text = null)
        {
            var toggle = controls.BindToggle(parent, position, get, set, text: text);
            if (text == null)
            {
                toggle.rectTransform.anchorMin = toggle.rectTransform.anchorMax = new Vector2(0.5f, 1);
                toggle.rectTransform.anchoredPosition = new Vector2(-16, -6);
            }
            var inputs = amountRoot.GetComponentsInChildren<TMP_InputField>(true);
            var dropdowns = amountRoot.GetComponentsInChildren<TMP_Dropdown>(true);
            controls.AddLateRefresh(() =>
            {
                bool available = enabled();
                toggle.SetDisabled(!available);
                bool editable = available && !get();
                foreach (var input in inputs) input.interactable = editable;
                foreach (var dropdown in dropdowns) dropdown.interactable = editable;
            });
        }
    }
}
