using System;
using System.Collections.Generic;
using System.Linq;

namespace DigitalLogistics.Buses
{
    internal sealed class TriggerBusInputWatcher : IDisposable
    {
        private static readonly Dictionary<GameItem, Action> liquidListeners = new();
        private readonly GameInventory[] inventories;
        private readonly GameItem liquidOwner;
        private readonly Action changed;
        private readonly HashSet<GameItem> items = new();

        internal TriggerBusInputWatcher(IEnumerable<GameInventory> inventories, GameItem liquidOwner, Action changed)
        {
            this.inventories = inventories.Distinct().ToArray();
            this.liquidOwner = liquidOwner;
            this.changed = changed;
            foreach (var inventory in this.inventories)
            {
                inventory.onSlotAddItemEndFunc += OnInventoryChanged;
                inventory.onSlotRemoveItemEndFunc += OnInventoryChanged;
                inventory.onSlotChangeAmountItemFunc += OnInventoryChanged;
            }
            UpdateItems();
        }

        private void OnInventoryChanged(GameItem item, GameInventory inventory, SlotMarker slot)
        {
            UpdateItems();
            changed();
        }

        private void UpdateItems()
        {
            var desired = inventories.SelectMany(inventory => inventory.childItems).ToHashSet();
            if (liquidOwner != null) desired.Add(liquidOwner);
            foreach (var item in items.Except(desired).ToArray()) Unwatch(item);
            foreach (var item in desired)
            {
                if (!items.Add(item)) continue;
                liquidListeners.TryGetValue(item, out var listeners);
                liquidListeners[item] = listeners + changed;
            }
        }

        private void Unwatch(GameItem item)
        {
            var listeners = liquidListeners[item] - changed;
            if (listeners == null) liquidListeners.Remove(item);
            else liquidListeners[item] = listeners;
            items.Remove(item);
        }

        internal static bool IsWatchedLiquidPart(GameItem item, string tag) =>
            liquidListeners.ContainsKey(item) && Liquid.Liquids.Any(liquid => liquid.currentPart == tag);

        internal static void NotifyLiquidChanged(GameItem item)
        {
            if (liquidListeners.TryGetValue(item, out var listeners)) listeners();
        }

        public void Dispose()
        {
            foreach (var inventory in inventories)
            {
                inventory.onSlotAddItemEndFunc -= OnInventoryChanged;
                inventory.onSlotRemoveItemEndFunc -= OnInventoryChanged;
                inventory.onSlotChangeAmountItemFunc -= OnInventoryChanged;
            }
            foreach (var item in items.ToArray()) Unwatch(item);
        }
    }
}
