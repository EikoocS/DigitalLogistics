using System;
using System.Collections.Generic;
using System.Linq;
using DigitalStorage.Storage;

namespace DigitalStorage.Item
{
    internal static class StorageBus
    {
        internal const string ItemId = "storage_bus";
        private const string InternalTag = "DIGITAL_STORAGE_BUS_INTERNAL_TAG";
        private static readonly Dictionary<GameItem, Connection> connections =
            new Dictionary<GameItem, Connection>();
        private static readonly Dictionary<GameInventory, (StorageProvider Provider, int References)> sharedProviders =
            new();

        internal static GameItem Create()
        {
            var item = ItemDirectory.CreateEmptyItem(ItemId)
                .SetSpriteAndShapeFromMod(DigitalStorage.Instance.Manifest.ID, ItemId)
                .SetShape(new GridShapeBuilder().SetDataFill(3, 1));
            item.shortDescription = StorageTerminal.Localize("item.storage_bus.desc");
            item.flavorText = StorageTerminal.Localize("item.storage_bus.flavor");
            item.mayToggleSlotItemFunc = (_, _, _) => true;
            item.canToggleSlotItemFunc = (_, _, _) => true;
            item.onToggleSlotItemFunc += Toggle;
            item.onSlotAddItemFunc += OnAdded;
            item.onSlotRemoveItemFunc += OnRemoved;
            item.onLoaded += OnLoaded;
            UpdateName(item);
            return item;
        }

        internal static void ClearConnections()
        {
            foreach (var connection in connections.Values) connection.Dispose();
            connections.Clear();
        }

        private static void UpdateName(GameItem item)
        {
            item.name = StorageTerminal.Localize(item.IsTag(InternalTag)
                ? "item.storage_bus.internal.name"
                : "item.storage_bus.external.name");
        }

        private static void OnLoaded(GameItem item)
        {
            UpdateName(item);
            if (item.parentInventory != null) OnAdded(item, item.parentInventory, null);
        }

        private static void OnAdded(GameItem item, GameInventory inventory, SlotMarker slot)
        {
            if (!DigitalStorage.Instance.Enabled) return;
            OnRemoved(item, inventory, slot);
            connections.Add(item, new Connection(item, inventory));
        }

        private static void OnRemoved(GameItem item, GameInventory inventory, SlotMarker slot)
        {
            if (connections.TryGetValue(item, out var connection))
            {
                connection.Dispose();
                connections.Remove(item);
            }
        }

        private static void Toggle(GameItem item, GameInventory inventory, SlotMarker slot)
        {
            item.SetEnableDisableTag(InternalTag, !item.IsTag(InternalTag));
            UpdateName(item);
            if (connections.TryGetValue(item, out var connection)) connection.Refresh();
        }

        private static void AcquireInventory(GameInventory inventory, GameItem owner)
        {
            if (sharedProviders.TryGetValue(inventory, out var shared))
            {
                shared.References++;
                sharedProviders[inventory] = shared;
                return;
            }
            var provider = new InventoryStorageProvider(inventory, owner);
            sharedProviders.Add(inventory, (provider, 1));
            DigitalStorage.Manager.registerStorageProvider(provider);
        }

        private static void ReleaseInventory(GameInventory inventory)
        {
            var shared = sharedProviders[inventory];
            if (--shared.References > 0)
            {
                sharedProviders[inventory] = shared;
                return;
            }
            sharedProviders.Remove(inventory);
            DigitalStorage.Manager.unregisterStorageProvider(shared.Provider);
        }

        private sealed class Connection : IDisposable
        {
            private readonly GameItem bus;
            private readonly GameInventory parent;
            private readonly HashSet<GameInventory> inventories = new();

            internal Connection(GameItem bus, GameInventory parent)
            {
                this.bus = bus;
                this.parent = parent;
                parent.onSlotAddItemEndFunc += OnInventoryChanged;
                parent.onSlotRemoveItemEndFunc += OnInventoryChanged;
                Refresh();
            }

            internal void Refresh()
            {
                var desired = new Dictionary<GameInventory, GameItem>();
                if (bus.IsTag(InternalTag))
                {
                    var owner = GraphUtils.FindParentType<GameItem>(parent);
                    if (InventoryStorageProvider.GetPanel(parent)?.role != "note")
                        desired.Add(parent, owner is GameCharacterItem ? null : owner);
                }
                else
                {
                    foreach (var target in FindTargets())
                        foreach (var inventory in GraphUtils.FindChildrenType<GameInventory>(target.contentWindow))
                            if (InventoryStorageProvider.GetPanel(inventory)?.role != "note")
                                desired[inventory] = target;
                }

                foreach (var inventory in inventories.ToArray())
                {
                    if (desired.ContainsKey(inventory)) continue;
                    ReleaseInventory(inventory);
                    inventories.Remove(inventory);
                }

                foreach (var entry in desired)
                    if (inventories.Add(entry.Key)) AcquireInventory(entry.Key, entry.Value);
            }

            private IEnumerable<GameItem> FindTargets()
            {
                if (!(parent is GameGridInventory) && !(parent is GameGridScrollableInventory)) yield break;
                var shape = bus.modifiedShape;
                foreach (var item in parent.childItems)
                {
                    if (ReferenceEquals(item, bus) || item.contentWindow == null) continue;
                    for (int column = 0; column < shape.width; column++)
                    {
                        var (x, y) = shape.ForwardTransform(column, -1);
                        if (!item.modifiedShape.Intersects(x, y)) continue;
                        yield return item;
                        break;
                    }
                }
            }

            private void Unregister()
            {
                foreach (var inventory in inventories) ReleaseInventory(inventory);
                inventories.Clear();
            }

            private void OnInventoryChanged(GameItem item, GameInventory inventory, SlotMarker slot) => Refresh();

            public void Dispose()
            {
                parent.onSlotAddItemEndFunc -= OnInventoryChanged;
                parent.onSlotRemoveItemEndFunc -= OnInventoryChanged;
                Unregister();
            }
        }
    }
}
