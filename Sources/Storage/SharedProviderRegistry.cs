using System;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Filtering;
using DigitalLogistics.Storage.Disks;
using DigitalLogistics.Storage.Providers;

namespace DigitalLogistics.Storage
{
    internal static class SharedProviderRegistry
    {
        internal sealed class InventoryLink
        {
            internal string Key;
            internal string Name;
            internal GameInventory Inventory;
            internal InventoryStorageProvider Provider;
            internal int References;
        }

        private static readonly Dictionary<GameInventory, InventoryLink> inventories = new();
        private static readonly Dictionary<Guid, (DiskStorageProvider Provider, int References)> disks = new();
        internal static IEnumerable<StorageProvider> Providers => inventories.Values.Select(link => (StorageProvider)link.Provider)
            .Concat(disks.Values.Select(link => link.Provider));

        internal static InventoryLink AcquireInventory(GameInventory inventory, GameItem owner)
        {
            if (!inventories.TryGetValue(inventory, out var link))
            {
                link = new InventoryLink
                {
                    Key = GetKey(inventory, owner),
                    Name = GetName(inventory, owner),
                    Inventory = inventory,
                    Provider = new InventoryStorageProvider(inventory)
                };
                inventories.Add(inventory, link);
            }
            link.References++;
            return link;
        }

        internal static void ReleaseInventory(InventoryLink link)
        {
            if (--link.References > 0) return;
            inventories.Remove(link.Inventory);
            link.Provider.Dispose();
        }

        internal static DiskStorageProvider AcquireDisk(Guid id, StorageDiskData data, int capacity,
            string name, StoredItemExpression filter)
        {
            if (disks.TryGetValue(id, out var link))
            {
                disks[id] = (link.Provider, link.References + 1);
                return link.Provider;
            }
            var provider = new DiskStorageProvider(id, data, capacity, name, filter);
            disks.Add(id, (provider, 1));
            return provider;
        }

        internal static DiskStorageProvider ReleaseDisk(Guid id)
        {
            var link = disks[id];
            if (link.References > 1)
            {
                disks[id] = (link.Provider, link.References - 1);
                return null;
            }
            disks.Remove(id);
            link.Provider.Dispose();
            return link.Provider;
        }

        internal static void Clear()
        {
            foreach (var provider in Providers.ToArray()) provider.Dispose();
            inventories.Clear();
            disks.Clear();
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
