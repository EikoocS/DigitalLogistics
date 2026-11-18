using DigitalLogistics.Item;
using DigitalLogistics.Buses;
using UnityEngine;
using static DigitalLogistics.UI.Common.MachineryControlFactory;
using static DigitalLogistics.UI.Buses.BusRowControls;

namespace DigitalLogistics.UI.Buses
{
    internal sealed class IOBusRow : IBusConfigurationRow
    {
        private readonly IOBusManager.Connection connection;
        public object Connection => connection;
        private GameItem Bus => connection.Bus;
        internal static int GetBaseHeight(GameItem bus) => bus.identifier == IOBus.InputOutputId ? 200 : 148;
        public int BaseHeight => GetBaseHeight(Bus);
        public int Height => BaseHeight + BusScheduleEditor.EntryHeight *
            ((IOBus.CanInput(Bus) ? connection.Configuration.InputSchedule.Count : 0)
                + (IOBus.CanOutput(Bus) ? connection.Configuration.OutputSchedule.Count : 0));

        internal IOBusRow(IOBusManager.Connection connection) => this.connection = connection;

        public void Build(BusRowControls controls)
        {
            var root = controls.Root;
            var configuration = connection.Configuration;
            const int countWidth = 120;
            var countLabel = CreateLabel(root, new Vector2(-Padding * 2 - countWidth, -Padding), DigitalLogistics.Localize("ui.io_bus.transfer_count"), anchor: 1);
            int countLabelWidth = FitLabel(countLabel);
            ((RectTransform)countLabel.root.transform).pivot = Vector2.one;
            controls.BindText(CreateInput(root, new Vector2(Padding + 48, -Padding), -Padding * 4 - 48 - countWidth - countLabelWidth),
                () => configuration.Name, () => connection.DefaultName, value => configuration.Name = value);
            controls.BindInteger(CreateInput(root, new Vector2(-Padding, -Padding), countWidth, right: true),
                () => configuration.RepeatCount, value => configuration.Count = Mathf.Max(1, value));
            string configurationKey = Bus.identifier switch
            {
                IOBus.InputId => "input_configuration",
                IOBus.OutputId => "output_configuration",
                _ => "filter_configuration"
            };
            int filterX = Padding * 2 + FitLabel(CreateLabel(root, new Vector2(Padding, -52), DigitalLogistics.Localize("ui.io_bus." + configurationKey)));
            controls.BindFilter(CreateInput(root, new Vector2(filterX, -52), -Padding - filterX),
                () => configuration.Filter, () => connection.DefaultFilter, value =>
                {
                    configuration.Filter = value;
                    IOBusManager.ApplyConfiguration(connection);
                });
            int y = 96;
            if (IOBus.CanInput(Bus))
                y = BusScheduleEditor.Build(controls, y, "input", configuration.InputSchedule, controls.Rebuild,
                    (cell, entry) => BindScheduleFilter(controls, cell, entry));
            if (IOBus.CanOutput(Bus))
                BusScheduleEditor.Build(controls, y, "output", configuration.OutputSchedule, controls.Rebuild,
                    (cell, entry) => BindScheduleFilter(controls, cell, entry));
            controls.BindEnabled(() => !configuration.Disabled, value => configuration.Disabled = !value);
        }

        private static void BindScheduleFilter(BusRowControls controls, RectTransform cell, IOBusScheduleEntry entry)
        {
            var input = CreateInput(cell, new Vector2(4, -4), -8);
            controls.BindText(input, () => entry.Filter, () => DigitalLogistics.Localize("ui.io_bus.inherit_filter"), value =>
            {
                bool valid = string.IsNullOrWhiteSpace(value) || ParseFilter(value) != null;
                input.textComponent.color = valid ? Color.white : Color.red;
                if (valid) entry.SetFilter(value.Trim());
            });
            input.onValueChanged.AddListener(_ => input.textComponent.color = Color.white);
        }
    }
}
