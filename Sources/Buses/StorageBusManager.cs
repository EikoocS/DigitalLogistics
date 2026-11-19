using System.Collections.Generic;
using DigitalLogistics.Filtering;
using DigitalLogistics.Storage;

namespace DigitalLogistics.Buses
{
    internal static class StorageBusManager
    {
        internal sealed class Connection
        {
            internal StorageBusConfiguration Configuration;
            internal SharedProviderRegistry.InventoryLink Link;
            internal int References;
            internal string DefaultName => Link.Name;
        }

        private static readonly Dictionary<GameInventory, Connection> active = new();

        internal static Connection GetConfiguration(GameInventory inventory) => active[inventory];

        internal static void Connect(SharedProviderRegistry.InventoryLink link)
        {
            if (!active.TryGetValue(link.Inventory, out var connection))
            {
                var configuration = BusConfigurationStore.GetTarget<StorageBusConfiguration>(link.Key, BusActions.ItemStorage);
                connection = new Connection { Configuration = configuration, Link = link };
                active.Add(link.Inventory, connection);
                ApplyConfiguration(connection);
            }
            connection.References++;
        }

        internal static void Disconnect(SharedProviderRegistry.InventoryLink link)
        {
            var connection = active[link.Inventory];
            if (--connection.References > 0) return;
            DigitalLogistics.Manager.Unregister(link.Provider);
            active.Remove(link.Inventory);
        }

        internal static void ApplyConfiguration(Connection connection)
        {
            var configuration = connection.Configuration;
            var provider = connection.Link.Provider;
            provider.Name = string.IsNullOrEmpty(configuration.Name) ? connection.DefaultName : configuration.Name;
            provider.Priority = configuration.Priority;
            provider.Filter = string.IsNullOrWhiteSpace(configuration.Filter)
                ? CachedItemExpression.Constant(true) : CachedItemExpressionParser.Parse(configuration.Filter);
            if (configuration.Disabled) DigitalLogistics.Manager.Unregister(provider);
            else DigitalLogistics.Manager.Register(provider);
            DigitalLogistics.Manager.NotifyChanged();
        }

        internal static void Clear()
        {
            foreach (var connection in active.Values) DigitalLogistics.Manager.Unregister(connection.Link.Provider);
            active.Clear();
        }
    }
}
