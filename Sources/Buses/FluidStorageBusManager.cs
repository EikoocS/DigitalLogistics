using System;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Filtering;
using DigitalLogistics.Storage;
using Newtonsoft.Json;

namespace DigitalLogistics.Buses
{
    internal static class FluidStorageBusManager
    {
        private const string DataKey = "fluid_storage_bus_configuration";

        internal sealed class Connection
        {
            internal StorageBusManager.Configuration Configuration;
            internal FluidProviderRegistry.Link Link;
            internal int References;
            internal string DefaultName => Link.Name;
        }

        private static readonly Dictionary<string, StorageBusManager.Configuration> configurations = new();
        private static readonly Dictionary<FluidProviderRegistry.Link, Connection> active = new();
        internal static Connection GetConfiguration(FluidProviderRegistry.Link link) => active[link];

        internal static void Connect(FluidProviderRegistry.Link link)
        {
            if (!active.TryGetValue(link, out var connection))
            {
                connection = new Connection { Link = link };
                if (link.Identity is not Guid)
                {
                    if (!configurations.TryGetValue(link.Key, out var configuration))
                        configurations.Add(link.Key, configuration = new StorageBusManager.Configuration { Key = link.Key });
                    connection.Configuration = configuration;
                }
                active.Add(link, connection);
                if (connection.Configuration == null) DigitalLogistics.Manager.Register(link.Provider);
                else ApplyConfiguration(connection);
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

        internal static void Load()
        {
            string json = ModHelper.GetModData(DigitalLogistics.Instance.Manifest.ID, DataKey);
            if (string.IsNullOrEmpty(json)) return;
            foreach (var configuration in JsonConvert.DeserializeObject<List<StorageBusManager.Configuration>>(json))
                configurations.Add(configuration.Key, configuration);
        }

        internal static void Save() => ModHelper.SetModData(DigitalLogistics.Instance.Manifest.ID, DataKey,
            JsonConvert.SerializeObject(active.Values.Select(connection => connection.Configuration)
                .Where(configuration => configuration != null).ToList()));

        internal static void Clear()
        {
            foreach (var connection in active.Values) DigitalLogistics.Manager.Unregister(connection.Link.Provider);
            active.Clear();
            configurations.Clear();
        }
    }
}
