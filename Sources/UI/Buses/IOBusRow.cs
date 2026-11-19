using System;
using System.Linq;
using DigitalLogistics.Buses;
using UnityEngine;
using static DigitalLogistics.UI.Common.MachineryControlFactory;
using static DigitalLogistics.UI.Buses.BusRowControls;

namespace DigitalLogistics.UI.Buses
{
    internal sealed class IOBusRow<T> : IBusConfigurationRow where T : FilteredBusScheduleEntry, new()
    {
        private readonly TransferBusConfiguration<T> configuration;
        private readonly Func<string> defaultName;
        private readonly string defaultFilter;
        private readonly FluidTransferBusConfiguration outputOptions;

        internal const int DefaultHeight = Padding * 2 + Line * 2 + BusScheduleEditor.HeaderHeight;
        public object Connection { get; }
        public int BaseHeight => DefaultHeight + (outputOptions == null ? 0 : Line);
        public int Height => BaseHeight + BusPlanStore.GetPlans(configuration).Count() * BusScheduleEditor.TransferEntryHeight;

        internal IOBusRow(object connection, TransferBusConfiguration<T> configuration,
            Func<string> defaultName, string defaultFilter, FluidTransferBusConfiguration outputOptions = null)
        {
            Connection = connection;
            this.configuration = configuration;
            this.defaultName = defaultName;
            this.defaultFilter = defaultFilter;
            this.outputOptions = outputOptions;
        }

        public void Build(BusRowControls controls)
        {
            var root = controls.Root;
            controls.BindText(CreateInput(root, new Vector2(NameX, -Padding), -NameX - Padding),
                () => configuration.Name, defaultName, value => configuration.Name = value);
            controls.BindFilter(BusScheduleEditor.CreateLabeledInput(root, Padding + Line, "ui.bus.filter"),
                () => configuration.Filter, () => defaultFilter, value => configuration.Filter = value);
            bool IsEnabled() => !configuration.Disabled;
            int y = Padding + Line * 2;
            if (outputOptions != null)
            {
                var avoidMixing = controls.BindToggle(root, new Vector2(Padding, -y), () => outputOptions.AvoidMixing,
                    value => outputOptions.AvoidMixing = value, text: DigitalLogistics.Localize("ui.fluid_bus.avoid_mixing"));
                controls.AddLateRefresh(() => avoidMixing.SetDisabled(!IsEnabled()));
                y += Line;
            }
            controls.BindEnabled(IsEnabled, value => configuration.Disabled = !value);
            bool fluid = typeof(T) == typeof(FluidBusScheduleEntry);
            BusScheduleEditor.Build(controls, y, configuration, IsEnabled,
                fluid ? "ui.bus.limit_volume" : "ui.bus.limit_quantity", "ui.bus.at_most", fluid ? 200 : 112,
                (amount, entry) =>
                {
                    switch (entry)
                    {
                        case FluidBusScheduleEntry liquid:
                            FluidVolumeControls.Bind(controls, amount, liquid.Amount);
                            break;
                        case IOBusScheduleEntry item:
                            controls.BindInteger(CreateInput(amount, Vector2.zero, 0),
                                () => item.Count, value => item.Count = Mathf.Max(1, value));
                            break;
                    }
                });
        }
    }
}
