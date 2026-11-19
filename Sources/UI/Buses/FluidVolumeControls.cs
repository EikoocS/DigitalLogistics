using DigitalLogistics.Buses;
using TMPro;
using UnityEngine;
using static DigitalLogistics.UI.Common.MachineryControlFactory;

namespace DigitalLogistics.UI.Buses
{
    internal static class FluidVolumeControls
    {
        internal static RectTransform Bind(BusRowControls controls, RectTransform parent, Vector2 position,
            int width, FluidVolume amount, bool right = false, bool stretch = false)
        {
            const int unitWidth = 76;
            var group = new GameObject("FluidVolume", typeof(RectTransform)).GetComponent<RectTransform>();
            group.SetParent(parent, false);
            group.anchorMin = group.pivot = new Vector2(right ? 1 : 0, 1);
            group.anchorMax = stretch ? Vector2.one : group.anchorMin;
            group.anchoredPosition = position;
            group.sizeDelta = new Vector2(width, 36);
            var input = CreateInput(group, Vector2.zero, -unitWidth - 4);
            input.contentType = TMP_InputField.ContentType.DecimalNumber;
            controls.BindText(input, () => amount.DisplayValue, () => string.Empty, value =>
                input.textComponent.color = amount.SetValue(value) ? Color.white : Color.red);
            input.onValueChanged.AddListener(_ => input.textComponent.color = Color.white);
            var root = CustomUIManager.Instance.SpawnElement(CustomUIManager.ElementKind.Dropdown, group, null, "FluidUnit");
            CustomUIMachineryStyle.Reskin(root);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(unitWidth, 36);
            var dropdown = CustomUIElement.Bind(root, "unit").dropdown;
            dropdown.ClearOptions();
            dropdown.AddOptions(new System.Collections.Generic.List<string>(FluidVolume.Units));
            dropdown.onValueChanged.AddListener(index =>
            {
                amount.Unit = (FluidVolumeUnit)index;
                input.SetTextWithoutNotify(amount.DisplayValue);
                input.textComponent.color = Color.white;
            });
            controls.AddRefresh(() => dropdown.SetValueWithoutNotify((int)amount.Unit));
            dropdown.SetValueWithoutNotify((int)amount.Unit);
            return group;
        }
    }
}
