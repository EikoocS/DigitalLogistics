using System;
using System.Collections.Generic;
using System.Linq;
using DigitalStorage.Filtering;
using UnityEngine;

namespace DigitalStorage.Storage
{
    internal static class BusManager
    {
        private const string DataKey = "storage_bus_configuration";

        [Serializable]
        internal sealed class Configuration
        {
            public string Key;
            public string Name = string.Empty;
            public bool Disabled;
            [NonSerialized] internal string DefaultName;
            public int Priority;
            public string Filter = string.Empty;
            [NonSerialized] internal InventoryStorageProvider Provider;
            [NonSerialized] internal int References;
            internal const string DefaultFilter = "True";

            internal void Apply()
            {
                if (Provider == null) return;
                Provider.Name = string.IsNullOrEmpty(Name) ? DefaultName : Name;
                Provider.Priority = Priority;
                Provider.filter = string.IsNullOrWhiteSpace(Filter)
                    ? StoredItemExpression.Constant(true) : StoredItemExpressionParser.Parse(Filter);
                if (Disabled) DigitalStorage.Manager.DetachStorageProvider(Provider);
                else DigitalStorage.Manager.registerStorageProvider(Provider);
                DigitalStorage.Manager.NotifyChanged();
            }
        }

        internal sealed class Link
        {
            internal string Key;
            internal string Name;
            internal GameInventory Inventory;
            internal InventoryStorageProvider Provider;
            internal int References;
            internal Configuration Configuration;
        }

        [Serializable]
        private sealed class SaveData
        {
            public List<Configuration> Configurations = new();
        }

        private static readonly Dictionary<string, Configuration> configurations = new();
        private static readonly Dictionary<GameInventory, Link> inventories = new();
        internal static IEnumerable<StorageProvider> Providers => inventories.Values.Select(link => link.Provider);

        internal static Configuration GetConfiguration(GameInventory inventory) => inventories[inventory].Configuration;

        internal static void Load()
        {
            string json = ModHelper.GetModData(DigitalStorage.Instance.Manifest.ID, DataKey);
            if (string.IsNullOrEmpty(json)) return;
            foreach (var configuration in JsonUtility.FromJson<SaveData>(json).Configurations)
                configurations.Add(configuration.Key, configuration);
        }

        internal static void Save()
        {
            var data = new SaveData
            {
                Configurations = configurations.Values.Where(config => config.References > 0).ToList()
            };
            ModHelper.SetModData(DigitalStorage.Instance.Manifest.ID, DataKey, JsonUtility.ToJson(data));
        }

        internal static void Clear()
        {
            foreach (var link in inventories.Values)
            {
                DigitalStorage.Manager.DetachStorageProvider(link.Provider);
                link.Provider.Dispose();
            }
            inventories.Clear();
            configurations.Clear();
        }

        internal static Link Acquire(GameInventory inventory, GameItem owner, bool storage)
        {
            if (!inventories.TryGetValue(inventory, out var link))
            {
                link = new Link
                {
                    Key = GetKey(inventory, owner),
                    Name = GetName(inventory, owner),
                    Inventory = inventory,
                    Provider = new InventoryStorageProvider(inventory)
                };
                inventories.Add(inventory, link);
            }
            link.References++;
            if (!storage) return link;
            if (link.Configuration == null)
            {
                if (!configurations.TryGetValue(link.Key, out var configuration))
                {
                    configuration = new Configuration { Key = link.Key };
                    configurations.Add(link.Key, configuration);
                }
                link.Configuration = configuration;
                configuration.DefaultName = link.Name;
                configuration.Provider = link.Provider;
                configuration.Apply();
            }
            link.Configuration.References++;
            return link;
        }

        internal static void Release(GameInventory inventory, bool storage)
        {
            var link = inventories[inventory];
            if (storage && --link.Configuration.References == 0)
            {
                DigitalStorage.Manager.DetachStorageProvider(link.Provider);
                link.Configuration.Provider = null;
                link.Configuration = null;
            }
            if (--link.References > 0) return;
            inventories.Remove(inventory);
            link.Provider.Dispose();
        }

        private static string GetKey(GameInventory inventory, GameItem owner)
        {
            var entry = EmporiumEntry.Instance;
            foreach (var field in typeof(EmporiumEntry).GetFields())
                if (typeof(GameInventory).IsAssignableFrom(field.FieldType)
                    && ReferenceEquals(field.GetValue(entry), inventory)) return "store:" + field.Name;
            foreach (var property in typeof(EmporiumEntry).GetProperties())
                if (typeof(GameInventory).IsAssignableFrom(property.PropertyType)
                    && ReferenceEquals(property.GetValue(entry), inventory)) return "store:" + property.Name;
            owner ??= inventory.GetParentItem();
            var queue = new Queue<GraphNodeStorage>();
            var visited = new HashSet<GraphNodeStorage>();
            queue.Enqueue(owner.contentWindow);
            int index = 0;
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (node is GameItem || !visited.Add(node)) continue;
                if (ReferenceEquals(node, inventory)) return owner.GetUniqueID() + ":" + index;
                if (node is GameInventory) index++;
                foreach (var child in node.children) queue.Enqueue(child);
            }
            throw new InvalidOperationException("Inventory is not part of its owner's content window.");
        }

        private static string GetName(GameInventory inventory, GameItem owner)
        {
            var panel = InventoryStorageProvider.GetPanel(inventory);
            string slotName = panel?.label?.GetText();
            var tabs = (inventory as PixelElement)?.handler?.FindParentElement<MachineryTabs>();
            if (tabs != null && tabs.count > 1)
            {
                for (int i = 0; i < tabs.count; i++)
                {
                    if (!MachineryContainerUtils.Collect(tabs.GetPage(i)).Contains(inventory)) continue;
                    slotName = tabs.GetCaption(i);
                    break;
                }
            }
            if (string.IsNullOrEmpty(slotName) && panel?.role == "power")
                slotName = LocHelper.GetLocalizedMechanic("mech_evaporator_label_battery");
            string itemName = (owner ?? inventory.GetParentItem())?.GetDisplayName(preferCustomName: true)
                ?? LocHelper.GetLocalizedMechanic("type_storage");
            return string.IsNullOrEmpty(slotName) ? itemName
                : string.IsNullOrEmpty(itemName) ? slotName : $"{itemName}.{slotName}";
        }
    }
}
