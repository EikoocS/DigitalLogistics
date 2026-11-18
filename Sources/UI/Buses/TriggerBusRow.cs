using System;
using System.Linq;
using TMPro;
using DigitalLogistics.Buses;
using UnityEngine;
using static DigitalLogistics.UI.Common.MachineryControlFactory;
using static DigitalLogistics.UI.Buses.BusRowControls;

namespace DigitalLogistics.UI.Buses
{
    internal sealed class TriggerBusRow : IBusConfigurationRow
    {
        private readonly TriggerBusManager.Connection connection;
        public object Connection => connection;
        internal const int DefaultHeight = 192;
        public int BaseHeight => DefaultHeight + (connection.Options.Length > 0 ? BusScheduleEditor.EntryHeight : 0);
        public int Height => BaseHeight + connection.Configuration.Schedule.Count * BusScheduleEditor.EntryHeight;

        internal TriggerBusRow(TriggerBusManager.Connection connection) => this.connection = connection;

        public void Build(BusRowControls controls)
        {
            var root = controls.Root;
            var configuration = connection.Configuration;
            var label = CreateLabel(root, new Vector2(Padding + 48, -Padding), string.Empty);
            ((RectTransform)label.root.transform).sizeDelta = new Vector2(Width * 2 - Padding * 4 - 48, 36);
            label.text.enableWordWrapping = false;
            label.text.overflowMode = TextOverflowModes.Ellipsis;
            label.text.richText = false;
            controls.AddRefresh(() => label.SetText(connection.Name));
            label.SetText(connection.Name);
            var inputToggle = controls.BindToggle(root, new Vector2(Padding, -52), () => configuration.OnInputUpdate,
                value => configuration.OnInputUpdate = value,
                text: DigitalLogistics.Localize("ui.trigger_bus.on_input_update"));
            var loopToggle = controls.BindToggle(root, new Vector2(Padding, -96), () => configuration.LoopTrigger,
                value => configuration.LoopTrigger = value,
                text: DigitalLogistics.Localize("ui.trigger_bus.loop_trigger"));
            int countX = 360 + Padding + FitLabel(CreateLabel(root, new Vector2(360, -96), DigitalLogistics.Localize("ui.io_bus.trigger_count")));
            var countInput = CreateInput(root, new Vector2(countX, -96), 128, stretch: false);
            controls.BindInteger(countInput, () => connection.TriggerCount, value =>
            {
                if (!configuration.LoopTrigger) configuration.Count = Mathf.Max(0, value);
            });
            var options = connection.Options;
            if (options.Length > 0)
            {
                int optionX = Padding * 2 + FitLabel(CreateLabel(root, new Vector2(Padding, -140), DigitalLogistics.Localize("ui.io_bus.trigger_configuration")));
                var dropdownRoot = CustomUIManager.Instance.SpawnElement(
                    CustomUIManager.ElementKind.Dropdown, root, null, "TriggerOption");
                CustomUIMachineryStyle.Reskin(dropdownRoot);
                var rect = (RectTransform)dropdownRoot.transform;
                rect.anchorMin = rect.pivot = new Vector2(0, 1);
                rect.anchorMax = Vector2.one;
                rect.anchoredPosition = new Vector2(optionX, -140);
                rect.sizeDelta = new Vector2(-Padding - optionX, 36);
                var dropdown = CustomUIElement.Bind(dropdownRoot, "option").dropdown;
                dropdown.ClearOptions();
                dropdown.AddOptions(options.Select(option => option.Name).ToList());
                dropdown.onValueChanged.AddListener(index => configuration.OptionId = options[index].Id);
                void RefreshOption() => dropdown.SetValueWithoutNotify(
                    Array.FindIndex(options, option => option.Id == configuration.OptionId));
                controls.AddRefresh(RefreshOption);
                RefreshOption();
            }
            BusScheduleEditor.Build(controls, options.Length > 0 ? 184 : 140, "trigger", configuration.Schedule, controls.Rebuild);
            controls.BindEnabled(() => !configuration.Disabled, value => configuration.Disabled = !value);
            controls.AddRefresh(() =>
            {
                countInput.interactable = !configuration.Disabled && !configuration.LoopTrigger;
                loopToggle.SetDisabled(configuration.Disabled);
                inputToggle.SetDisabled(configuration.Disabled);
            });
        }
    }
}
