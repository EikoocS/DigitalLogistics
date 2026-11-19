using DigitalLogistics.Buses;
using UnityEngine;
using static DigitalLogistics.UI.Common.MachineryControlFactory;
using static DigitalLogistics.UI.Buses.BusRowControls;

namespace DigitalLogistics.UI.Buses
{
    internal sealed class StorageBusRow : IBusConfigurationRow
    {
        private readonly StorageBusConfiguration configuration;
        private readonly System.Func<string> defaultName;
        private readonly System.Action apply;
        public object Connection { get; }
        internal const int DefaultHeight = 96;
        public int BaseHeight => DefaultHeight;
        public int Height => BaseHeight;

        internal StorageBusRow(StorageBusManager.Connection connection)
            : this(connection, connection.Configuration, () => connection.DefaultName,
                () => StorageBusManager.ApplyConfiguration(connection)) { }

        internal StorageBusRow(FluidStorageBusManager.Connection connection)
            : this(connection, connection.Configuration, () => connection.DefaultName,
                () => FluidStorageBusManager.ApplyConfiguration(connection)) { }

        private StorageBusRow(object connection, StorageBusConfiguration configuration,
            System.Func<string> defaultName, System.Action apply)
        {
            Connection = connection;
            this.configuration = configuration;
            this.defaultName = defaultName;
            this.apply = apply;
        }

        public void Build(BusRowControls controls)
        {
            var root = controls.Root;
            var priorityLabel = CreateLabel(root, new Vector2(-Padding - 120 - Padding, -Padding),
                DigitalLogistics.Localize("ui.io_bus.storage_priority"), anchor: 1);
            int priorityLabelWidth = FitLabel(priorityLabel);
            ((RectTransform)priorityLabel.root.transform).pivot = Vector2.one;
            controls.BindText(CreateInput(root, new Vector2(Padding + 48, -Padding), -192 - priorityLabelWidth - Padding),
                () => configuration.Name, defaultName, value =>
                {
                    configuration.Name = value;
                    apply();
                });
            controls.BindInteger(CreateInput(root, new Vector2(-Padding, -Padding), 120, right: true),
                () => configuration.Priority, value =>
                {
                    configuration.Priority = value;
                    apply();
                });
            int filterX = Padding * 2 + FitLabel(CreateLabel(root, new Vector2(Padding, -52), DigitalLogistics.Localize("ui.io_bus.storage_configuration")));
            controls.BindFilter(CreateInput(root, new Vector2(filterX, -52), -Padding - filterX),
                () => configuration.Filter, () => StorageBusConfiguration.DefaultFilter, value =>
                {
                    configuration.Filter = value;
                    apply();
                });
            controls.BindEnabled(() => !configuration.Disabled, value =>
            {
                configuration.Disabled = !value;
                apply();
            });
        }
    }
}
