using DigitalLogistics.Buses;
using UnityEngine;
using static DigitalLogistics.UI.Common.MachineryControlFactory;
using static DigitalLogistics.UI.Buses.BusRowControls;

namespace DigitalLogistics.UI.Buses
{
    internal sealed class StorageBusRow : IBusConfigurationRow
    {
        private readonly StorageBusManager.Connection connection;
        public object Connection => connection;
        internal const int DefaultHeight = 96;
        public int BaseHeight => DefaultHeight;
        public int Height => BaseHeight;

        internal StorageBusRow(StorageBusManager.Connection connection) => this.connection = connection;

        public void Build(BusRowControls controls)
        {
            var root = controls.Root;
            var configuration = connection.Configuration;
            var priorityLabel = CreateLabel(root, new Vector2(-Padding - 120 - Padding, -Padding),
                DigitalLogistics.Localize("ui.io_bus.storage_priority"), anchor: 1);
            int priorityLabelWidth = FitLabel(priorityLabel);
            ((RectTransform)priorityLabel.root.transform).pivot = Vector2.one;
            controls.BindText(CreateInput(root, new Vector2(Padding + 48, -Padding), -192 - priorityLabelWidth - Padding),
                () => configuration.Name, () => connection.DefaultName, value =>
                {
                    configuration.Name = value;
                    StorageBusManager.ApplyConfiguration(connection);
                });
            controls.BindInteger(CreateInput(root, new Vector2(-Padding, -Padding), 120, right: true),
                () => configuration.Priority, value =>
                {
                    configuration.Priority = value;
                    StorageBusManager.ApplyConfiguration(connection);
                });
            int filterX = Padding * 2 + FitLabel(CreateLabel(root, new Vector2(Padding, -52), DigitalLogistics.Localize("ui.io_bus.storage_configuration")));
            controls.BindFilter(CreateInput(root, new Vector2(filterX, -52), -Padding - filterX),
                () => configuration.Filter, () => StorageBusManager.Configuration.DefaultFilter, value =>
                {
                    configuration.Filter = value;
                    StorageBusManager.ApplyConfiguration(connection);
                });
            controls.BindEnabled(() => !configuration.Disabled, value =>
            {
                configuration.Disabled = !value;
                StorageBusManager.ApplyConfiguration(connection);
            });
        }
    }
}
