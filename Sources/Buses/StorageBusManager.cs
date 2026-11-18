using System;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Filtering;
using DigitalLogistics.Item;
using DigitalLogistics.Storage;
using Newtonsoft.Json;

namespace DigitalLogistics.Buses
{
    internal static class StorageBusManager
    {
        private const string DataKey = "storage_bus_configuration";

        [Serializable]
        internal sealed class Configuration
        {
            public string Key;
            public string Name = string.Empty;
            public bool Disabled;
            public int Priority;
            public string Filter = string.Empty;
            internal const string DefaultFilter = "True";
        }

        internal sealed class Connection
        {
            internal Configuration Configuration;
            internal SharedProviderRegistry.InventoryLink Link;
            internal int References;
            internal string DefaultName => Link.Name;
        }

        [Serializable]
        private sealed class SaveData
        {
            public List<Configuration> Configurations = new();
        }

        private static readonly Dictionary<string, Configuration> configurations = new();
        private static readonly Dictionary<GameInventory, Connection> active = new();

        internal static Connection GetConfiguration(GameInventory inventory) => active[inventory];

        internal static void Connect(SharedProviderRegistry.InventoryLink link)
        {
            if (!active.TryGetValue(link.Inventory, out var connection))
            {
                if (!configurations.TryGetValue(link.Key, out var configuration))
                    configurations.Add(link.Key, configuration = new Configuration { Key = link.Key });
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
                ? StoredItemExpression.Constant(true) : StoredItemExpressionParser.Parse(configuration.Filter);
            if (configuration.Disabled) DigitalLogistics.Manager.Unregister(provider);
            else DigitalLogistics.Manager.Register(provider);
            DigitalLogistics.Manager.NotifyChanged();
        }

        internal static Guid? ConnectDisk(ItemDisk disk)
        {
            if (BusAccess.IsBlocked(disk)) return null;
            var data = disk.GetStorage();
            if (data == null) return null;
            var provider = SharedProviderRegistry.AcquireDisk(disk.DiskId.Value, data, disk.Capacity,
                disk.DiskName, disk.Filter, disk);
            DigitalLogistics.Manager.Register(provider);
            return disk.DiskId.Value;
        }

        internal static void DisconnectDisk(Guid id, ItemDisk disk)
        {
            var provider = SharedProviderRegistry.ReleaseDisk(id, disk);
            if (provider != null) DigitalLogistics.Manager.Unregister(provider);
        }

        internal static void Load()
        {
            string json = ModHelper.GetModData(DigitalLogistics.Instance.Manifest.ID, DataKey);
            if (string.IsNullOrEmpty(json)) return;
            foreach (var configuration in JsonConvert.DeserializeObject<SaveData>(json).Configurations)
                configurations.Add(configuration.Key, configuration);
        }

        internal static void Save() => ModHelper.SetModData(DigitalLogistics.Instance.Manifest.ID, DataKey,
            JsonConvert.SerializeObject(new SaveData
            {
                Configurations = active.Values.Select(connection => connection.Configuration).ToList()
            }));

        internal static void Clear()
        {
            foreach (var connection in active.Values) DigitalLogistics.Manager.Unregister(connection.Link.Provider);
            active.Clear();
            configurations.Clear();
        }
    }
}
