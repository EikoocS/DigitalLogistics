using DigitalLogistics.Buses;
using DigitalLogistics.Item;
using UnityEngine;
using static DigitalLogistics.UI.Common.MachineryControlFactory;
using static DigitalLogistics.UI.Buses.BusRowControls;

namespace DigitalLogistics.UI.Buses
{
    internal sealed class FluidIOBusRow : IBusConfigurationRow
    {
        private readonly FluidIOBusManager.Connection connection;
        public object Connection => connection;
        private GameItem Bus => connection.Bus;
        internal static int GetBaseHeight(GameItem bus) => bus.identifier == FluidIOBus.InputOutputId ? 244 : 192;
        public int BaseHeight => GetBaseHeight(Bus);
        public int Height => BaseHeight + BusScheduleEditor.EntryHeight *
            ((FluidIOBus.CanInput(Bus) ? connection.Configuration.InputSchedule.Count : 0)
                + (FluidIOBus.CanOutput(Bus) ? connection.Configuration.OutputSchedule.Count : 0));

        internal FluidIOBusRow(FluidIOBusManager.Connection connection) => this.connection = connection;

        public void Build(BusRowControls controls)
        {
            var root = controls.Root;
            var configuration = connection.Configuration;
            const int amountWidth = 200;
            var amountLabel = CreateLabel(root, new Vector2(-Padding * 2 - amountWidth, -Padding),
                DigitalLogistics.Localize("ui.fluid_bus.transfer_volume"), anchor: 1);
            int labelWidth = FitLabel(amountLabel);
            ((RectTransform)amountLabel.root.transform).pivot = Vector2.one;
            controls.BindText(CreateInput(root, new Vector2(Padding + 48, -Padding),
                -Padding * 4 - 48 - amountWidth - labelWidth),
                () => configuration.Name, () => connection.DefaultName, value => configuration.Name = value);
            var amountControls = FluidVolumeControls.Bind(controls, root, new Vector2(-Padding, -Padding),
                amountWidth, configuration.Amount, right: true);
            string key = Bus.identifier switch
            {
                FluidIOBus.InputId => "input_configuration",
                FluidIOBus.OutputId => "output_configuration",
                _ => "filter_configuration"
            };
            int filterX = Padding * 2 + FitLabel(CreateLabel(root, new Vector2(Padding, -52),
                DigitalLogistics.Localize("ui.io_bus." + key)));
            controls.BindFilter(CreateInput(root, new Vector2(filterX, -52), -Padding - filterX),
                () => configuration.Filter, () => connection.DefaultFilter, value =>
                {
                    configuration.Filter = value;
                    FluidIOBusManager.ApplyConfiguration(connection);
                });
            UnlimitedTransferControls.Bind(controls, root, new Vector2(Padding, -96),
                () => configuration.UnlimitedTransfer, value => configuration.UnlimitedTransfer = value,
                () => !configuration.Disabled, amountControls,
                DigitalLogistics.Localize("ui.io_bus.unlimited_transfer"));
            int y = 140;
            if (FluidIOBus.CanInput(Bus))
                y = BuildSchedule(controls, y, "input", configuration.InputSchedule);
            if (FluidIOBus.CanOutput(Bus)) BuildSchedule(controls, y, "output", configuration.OutputSchedule);
            controls.BindEnabled(() => !configuration.Disabled, value => configuration.Disabled = !value);
        }

        private int BuildSchedule(BusRowControls controls, int y, string direction,
            System.Collections.Generic.List<FluidBusScheduleEntry> schedule) =>
            BusScheduleEditor.Build(controls, y, direction, schedule, controls.Rebuild,
                (cell, entry) =>
                {
                    var input = CreateInput(cell, new Vector2(4, -4), -8);
                    controls.BindText(input, () => entry.Filter,
                        () => DigitalLogistics.Localize("ui.io_bus.inherit_filter"), value =>
                        {
                            bool valid = string.IsNullOrWhiteSpace(value) || ParseFilter(value) != null;
                            input.textComponent.color = valid ? Color.white : Color.red;
                            if (valid) entry.SetFilter(value.Trim());
                        });
                    input.onValueChanged.AddListener(_ => input.textComponent.color = Color.white);
                },
                (cell, entry) => FluidVolumeControls.Bind(controls, cell, new Vector2(4, -4), -8, entry.Amount, stretch: true),
                "schedule_volume", (cell, entry, amountCell) => UnlimitedTransferControls.Bind(
                    controls, cell, Vector2.zero, () => entry.UnlimitedTransfer,
                    value => entry.UnlimitedTransfer = value,
                    () => !connection.Configuration.Disabled && entry.Enabled, amountCell));
    }
}
