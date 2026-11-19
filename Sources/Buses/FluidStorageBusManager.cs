using System.Collections.Generic;
using DigitalLogistics.Filtering;
using DigitalLogistics.Storage;

namespace DigitalLogistics.Buses
{
    internal static class FluidStorageBusManager
    {
        internal sealed class Connection
        {
            internal StorageBusConfiguration Configuration;
            internal FluidProviderRegistry.Link Link;
            internal int References;
            internal string DefaultName => Link.Name;
        }

        private static readonly Dictionary<FluidProviderRegistry.Link, Connection> active = new();
        internal static Connection GetConfiguration(FluidProviderRegistry.Link link) => active[link];

        internal static void Connect(FluidProviderRegistry.Link link)
        {
            if (!active.TryGetValue(link, out var connection))
            {
                var configuration = BusConfigurationStore.GetTarget<StorageBusConfiguration>(link.Key, BusActions.FluidStorage);
                connection = new Connection { Link = link, Configuration = configuration };
                active.Add(link, connection);
                ApplyConfiguration(connection);
            }
            connection.References++;
        }

        internal static void Disconnect(FluidProviderRegistry.Link link)
        {
            var connection = active[link];
            if (--connection.References > 0) return;
            DigitalLogistics.Manager.Unregister(link.Provider);
            active.Remove(link);
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
