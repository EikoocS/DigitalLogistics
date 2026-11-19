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
        internal const int DefaultHeight = Padding * 2 + Line + BusScheduleEditor.HeaderHeight;
        public int BaseHeight => DefaultHeight + (connection.Options.Length > 0 ? Line : 0)
            + (connection.Action.SubscribeInputs != null ? Line : 0);
        public int Height => BaseHeight + connection.Configuration.Schedule.Count * BusScheduleEditor.TriggerEntryHeight;

        internal TriggerBusRow(TriggerBusManager.Connection connection) => this.connection = connection;

        public void Build(BusRowControls controls)
        {
            var root = controls.Root;
            var configuration = connection.Configuration;
            var label = CreateLabel(root, new Vector2(NameX, -Padding), connection.Name);
            var nameRect = (RectTransform)label.root.transform;
            nameRect.anchorMax = Vector2.one;
            nameRect.sizeDelta = new Vector2(-NameX - Padding, 36);
            label.text.enableWordWrapping = false;
            label.text.overflowMode = TextOverflowModes.Ellipsis;
            label.text.richText = false;
            controls.AddRefresh(() => label.SetText(connection.Name));

            bool IsEnabled() => !configuration.Disabled;
            int y = Padding + Line;
            if (connection.Action.SubscribeInputs != null)
            {
                var autoTrigger = controls.BindToggle(root, new Vector2(Padding, -y), () => configuration.OnInputUpdate,
                    value => configuration.OnInputUpdate = value,
                    text: DigitalLogistics.Localize("ui.trigger_bus.on_input_update"));
                controls.AddLateRefresh(() => autoTrigger.SetDisabled(!IsEnabled()));
                y += Line;
            }
            var options = connection.Options;
            if (options.Length > 0)
            {
                BindOption(controls, y, options);
                y += Line;
            }
            controls.BindEnabled(IsEnabled, value => configuration.Disabled = !value);
            BusScheduleEditor.Build(controls, y, configuration.Schedule, IsEnabled,
                "ui.trigger_bus.limit_count", "ui.trigger_bus.at_most", 112, (amount, entry) =>
                    controls.BindInteger(CreateInput(amount, Vector2.zero, 0),
                        () => entry.Count, value => entry.Count = Mathf.Max(1, value)));
        }

        private void BindOption(BusRowControls controls, int y, TriggerBusAction.Option[] options)
        {
            var root = controls.Root;
            int x = Padding * 2 + FitLabel(CreateLabel(root, new Vector2(Padding, -y),
                DigitalLogistics.Localize("ui.trigger_bus.configuration")));
            var dropdownRoot = CustomUIManager.Instance.SpawnElement(
                CustomUIManager.ElementKind.Dropdown, root, null, "TriggerOption");
            CustomUIMachineryStyle.Reskin(dropdownRoot);
            var rect = (RectTransform)dropdownRoot.transform;
            rect.anchorMin = rect.pivot = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(-Padding - x, 36);
            var dropdown = CustomUIElement.Bind(dropdownRoot, "option").dropdown;
            dropdown.ClearOptions();
            dropdown.AddOptions(options.Select(option => option.Name).ToList());
            dropdown.onValueChanged.AddListener(index => connection.Configuration.OptionId = options[index].Id);
            void Refresh() => dropdown.SetValueWithoutNotify(
                Array.FindIndex(options, option => option.Id == connection.Configuration.OptionId));
            controls.AddRefresh(Refresh);
            Refresh();
        }
    }
}
