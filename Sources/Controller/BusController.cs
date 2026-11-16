using System;
using System.Collections.Generic;
using System.Linq;
using DigitalStorage.Item;
using DigitalStorage.Storage;
using DigitalStorage.UI;

namespace DigitalStorage.Controller
{
    internal static class BusController
    {
        private static readonly Dictionary<GameItem, Connection> connections =
            new Dictionary<GameItem, Connection>();
        internal static void ClearConnections()
        {
            StorageIndexRefresh.Clear();
            foreach (var connection in connections.Values) connection.Dispose();
            connections.Clear();
            IOBusManager.Clear();
            BusManager.Clear();
        }

        internal static void OnLoaded(GameItem item)
        {
            UpdateName(item);
            if (item.parentInventory != null) Connect(item, item.parentInventory);
        }

        internal static void OnAdded(GameItem item, GameInventory inventory, SlotMarker slot)
        {
            Connect(item, inventory);
            if (IOBus.IsIO(item) && !SaveManager.IsDecoding && IOBusManager.IsOnMove(item))
                IOBusManager.Schedule(item);
        }

        private static void Connect(GameItem item, GameInventory inventory)
        {
            if (!DigitalStorage.Instance.Enabled) return;
            OnRemoved(item, inventory, null);
            var connection = new Connection(item, inventory);
            connections.Add(item, connection);
            connection.Refresh();
        }

        internal static void OnRemoved(GameItem item, GameInventory inventory, SlotMarker slot)
        {
            if (connections.TryGetValue(item, out var connection))
            {
                connection.Dispose();
                connections.Remove(item);
            }
        }

        internal static void Toggle(GameItem item, GameInventory inventory, SlotMarker slot)
        {
            if (item.IsTag(StorageBus.DisabledTag))
                item.DisableTag(StorageBus.DisabledTag);
            else if (item.IsTag(StorageBus.InternalTag))
            {
                item.DisableTag(StorageBus.InternalTag);
                item.EnableTag(StorageBus.DisabledTag);
            }
            else item.EnableTag(StorageBus.InternalTag);
            UpdateName(item);
            if (connections.TryGetValue(item, out var connection)) connection.Refresh();
        }

        internal static IEnumerable<BusManager.Configuration> GetConfigurations(GameItem bus) =>
            connections.TryGetValue(bus, out var connection)
                ? connection.Inventories.Select(BusManager.GetConfiguration)
                : Enumerable.Empty<BusManager.Configuration>();

        private static void UpdateName(GameItem item)
        {
            if (IOBus.IsIO(item)) IOBus.UpdateName(item);
            else StorageBus.UpdateName(item);
        }

        private sealed class Connection : IDisposable
        {
            private readonly GameItem bus;
            private readonly GameInventory parent;
            private bool IsStorage => !IOBus.IsIO(bus);
            private readonly HashSet<GameInventory> inventories = new();
            internal IEnumerable<GameInventory> Inventories => inventories;

            internal Connection(GameItem bus, GameInventory parent)
            {
                this.bus = bus;
                this.parent = parent;
                parent.onSlotAddItemEndFunc += OnInventoryChanged;
                parent.onSlotRemoveItemEndFunc += OnInventoryChanged;
            }

            internal void Refresh()
            {
                var desired = new Dictionary<GameInventory, GameItem>();
                if (!bus.IsTag(StorageBus.DisabledTag) && bus.IsTag(StorageBus.InternalTag))
                {
                    var owner = GraphUtils.FindParentType<GameItem>(parent);
                    if (InventoryStorageProvider.GetPanel(parent)?.role != "note")
                        desired.Add(parent, owner is GameCharacterItem ? null : owner);
                }
                else if (!bus.IsTag(StorageBus.DisabledTag))
                {
                    foreach (var target in FindTargets())
                        foreach (var inventory in GraphUtils.FindChildrenType<GameInventory>(target.contentWindow))
                            if (InventoryStorageProvider.GetPanel(inventory)?.role != "note")
                                desired[inventory] = target;
                }

                foreach (var inventory in inventories.ToArray())
                {
                    if (desired.ContainsKey(inventory)) continue;
                    if (!IsStorage) IOBusManager.Release(bus, inventory);
                    BusManager.Release(inventory, IsStorage);
                    inventories.Remove(inventory);
                }

                foreach (var entry in desired)
                {
                    if (inventories.Contains(entry.Key)) continue;
                    var link = BusManager.Acquire(entry.Key, entry.Value, IsStorage);
                    if (!IsStorage) IOBusManager.Acquire(bus, link);
                    inventories.Add(entry.Key);
                }

                if (inventories.Count == 0 && IsStorage) bus.contentWindow.Lock();
                else if (bus.contentWindow.isLocked) bus.contentWindow.Unlock();
                BusUI.RefreshConnections(bus);
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
                foreach (var inventory in inventories)
                {
                    if (!IsStorage) IOBusManager.Release(bus, inventory);
                    BusManager.Release(inventory, IsStorage);
                }
                inventories.Clear();
            }

            private void OnInventoryChanged(GameItem item, GameInventory inventory, SlotMarker slot) => Refresh();

            public void Dispose()
            {
                parent.onSlotAddItemEndFunc -= OnInventoryChanged;
                parent.onSlotRemoveItemEndFunc -= OnInventoryChanged;
                Unregister();
                bus.contentWindow.Lock();
            }
        }
    }
}
