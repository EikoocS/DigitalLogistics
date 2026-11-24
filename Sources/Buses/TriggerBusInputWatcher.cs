using System;
using System.Collections.Generic;
using System.Linq;

namespace DigitalLogistics.Buses
{
    internal sealed class TriggerBusInputWatcher : IDisposable
    {
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
                LiquidChangeEvents.Subscribe(item, changed);
            }
        }

        private void Unwatch(GameItem item)
        {
            LiquidChangeEvents.Unsubscribe(item, changed);
            items.Remove(item);
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
