using System;
using System.Collections.Generic;
using System.Linq;
using DigitalStorage.Item;
using DigitalStorage.Storage;

namespace DigitalStorage.Controller
{
    internal static class StorageBusController
    {
        private static readonly Dictionary<GameItem, Connection> connections =
            new Dictionary<GameItem, Connection>();
        private static readonly Dictionary<GameInventory, (StorageProvider Provider, int References)> sharedProviders =
            new();

        internal static void ClearConnections()
        {
            foreach (var connection in connections.Values) connection.Dispose();
            connections.Clear();
        }

        internal static void OnLoaded(GameItem item)
        {
            StorageBus.UpdateName(item);
            if (item.parentInventory != null) OnAdded(item, item.parentInventory, null);
        }

        internal static void OnAdded(GameItem item, GameInventory inventory, SlotMarker slot)
        {
            if (!DigitalStorage.Instance.Enabled) return;
            OnRemoved(item, inventory, slot);
            connections.Add(item, new Connection(item, inventory));
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
            item.SetEnableDisableTag(StorageBus.InternalTag, !item.IsTag(StorageBus.InternalTag));
            StorageBus.UpdateName(item);
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
            var provider = new InventoryStorageProvider(inventory, GetProviderName(inventory, owner));
            sharedProviders.Add(inventory, (provider, 1));
            DigitalStorage.Manager.registerStorageProvider(provider);
        }

        private static string GetProviderName(GameInventory inventory, GameItem owner)
        {
            var panel = InventoryStorageProvider.GetPanel(inventory);
            string slotName = panel?.role == "power"
                ? StorageTerminal.Localize("storage.slot.energy")
                : panel?.label?.GetText();
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
            if (owner == null) return slotName ?? string.Empty;
            string itemName = owner.GetDisplayName(preferCustomName: true);
            return string.IsNullOrEmpty(slotName) ? itemName : $"{itemName}：{slotName}";
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
                if (bus.IsTag(StorageBus.InternalTag))
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
