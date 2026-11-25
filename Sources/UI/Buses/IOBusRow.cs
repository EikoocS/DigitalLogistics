using System;
using DigitalLogistics.Buses;
using UnityEngine;
using static DigitalLogistics.UI.Common.MachineryControlFactory;
using static DigitalLogistics.UI.Buses.BusRowControls;

namespace DigitalLogistics.UI.Buses
{
    internal sealed class IOBusRow : IBusConfigurationRow
    {
        private readonly TransferBusConfiguration configuration;
        private readonly Func<string> defaultName;
        private readonly string defaultFilter;
        private readonly FluidTransferBusConfiguration outputOptions;

        internal const int DefaultHeight = Padding * 2 + Line * 2;
        public object Connection { get; }
        public int Height => DefaultHeight + (outputOptions == null ? 0 : Line);

        internal IOBusRow(object connection, TransferBusConfiguration configuration,
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
            int filterY = Padding + Line;
            int filterX = Padding * 2 + FitLabel(CreateLabel(root, new Vector2(Padding, -filterY),
                DigitalLogistics.Localize("ui.common.filter")));
            controls.BindFilter(CreateInput(root, new Vector2(filterX, -filterY), -Padding - filterX),
                () => configuration.Filter, () => defaultFilter, value =>
                {
                    configuration.Filter = value;
                    LogisticsScheduler.Refresh(configuration);
                });
            bool IsEnabled() => !configuration.Disabled;
            int y = Padding + Line * 2;
            if (outputOptions != null)
            {
                var avoidMixing = controls.BindToggle(root, new Vector2(Padding, -y), () => outputOptions.AvoidMixing,
                    value =>
                    {
                        outputOptions.AvoidMixing = value;
                        LogisticsScheduler.Refresh(configuration);
                    }, text: DigitalLogistics.Localize("ui.fluid_bus.avoid_mixing"));
                controls.AddLateRefresh(() => avoidMixing.SetDisabled(!IsEnabled()));
            }
            controls.BindEnabled(IsEnabled, value =>
            {
                configuration.Disabled = !value;
                LogisticsScheduler.Refresh(configuration);
            });
        }
    }
}
