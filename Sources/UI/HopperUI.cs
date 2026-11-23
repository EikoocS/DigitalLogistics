using System;
using System.Linq;
using DigitalLogistics.Hoppers;
using DigitalLogistics.Item;
using DigitalLogistics.UI.Common;
using TMPro;
using UnityEngine;
using static DigitalLogistics.UI.Common.MachineryControlFactory;

namespace DigitalLogistics.UI
{
    internal sealed class HopperUI
    {
        private const int Padding = 8;
        private const int Line = 44;
        private readonly Hopper hopper;
        private readonly TMP_Dropdown input;
        private readonly TMP_Dropdown output;
        private readonly RectTransform controls;
        private readonly Action refreshFilter;
        internal PixelWindow Window { get; }

        internal HopperUI(Hopper hopper)
        {
            this.hopper = hopper;
            const int bodyHeight = Padding * 2 + 3 * Line;
            var (host, root) = CreateControlGroup(260, bodyHeight / 2);
            controls = root;
            input = CreateSlotDropdown(hopper.Input, "ui.hopper.input_slot", Padding);
            output = CreateSlotDropdown(hopper.Output, "ui.hopper.output_slot", Padding + Line);
            int y = Padding + 2 * Line;
            int x = LabelEnd("ui.bus.filter", y);
            var filter = CreateInput(controls, new Vector2(x, -y), -Padding - x);
            refreshFilter = MachineryInputBinding.BindFilter(filter, () => hopper.Configuration.Filter, () => HopperConfiguration.DefaultFilter,
                value => hopper.Configuration.Filter = value);
            Window = MachineHelper.CreateMachineryWindow(true, MachineHelper.Panel(null, host));
            Refresh();
        }

        private int LabelEnd(string key, int y) => Padding * 2 + FitLabel(
            CreateLabel(controls, new Vector2(Padding, -y), DigitalLogistics.Localize(key)));

        private TMP_Dropdown CreateSlotDropdown(HopperPort port, string key, int y)
        {
            int x = LabelEnd(key, y);
            var root = CustomUIManager.Instance.SpawnElement(CustomUIManager.ElementKind.Dropdown,
                controls, null, key);
            CustomUIMachineryStyle.Reskin(root);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.pivot = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(-Padding - x, 36);
            var dropdown = CustomUIElement.Bind(root, "slot").dropdown;
            dropdown.onValueChanged.AddListener(index => hopper.SelectSlot(port, index));
            return dropdown;
        }

        internal void Refresh()
        {
            RefreshSlot(input, hopper.Input);
            RefreshSlot(output, hopper.Output);
            refreshFilter();
        }

        private static void RefreshSlot(TMP_Dropdown dropdown, HopperPort port)
        {
            var options = port.GetSlotNames();
            bool hasSlot = options.Count > 0;
            if (!hasSlot) options.Add("—");
            if (!dropdown.options.Select(option => option.text).SequenceEqual(options))
            {
                dropdown.Hide();
                dropdown.ClearOptions();
                dropdown.AddOptions(options);
            }
            dropdown.interactable = hasSlot;
            dropdown.SetValueWithoutNotify(hasSlot ? port.Index : 0);
        }
    }
}
