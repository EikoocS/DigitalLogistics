using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalStorage.Storage
{
    // StorageProvider bound to GameInventory
    public class InventoryStorageProvider : StorageProvider
    {
        private const double SearchBudgetMilliseconds = 2;
        private readonly GameInventory inventory;
        private readonly string name;
        private readonly GameItem nameSource;
        public string Name
        {
            get
            {
                if (nameSource == null) return name;
                var itemName = nameSource.GetDisplayName(preferCustomName: true);
                var panel = GetPanel(inventory);
                var slotName = panel?.role == "power"
                    ? ModHelper.GetLocalized(DigitalStorage.Instance.Manifest.ID, "storage.slot.energy")
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
                return string.IsNullOrEmpty(slotName) ? itemName : $"{itemName}：{slotName}";
            }
        }

        private readonly StorageIndex<PhysicalStoredItem> displayNameIndex = new(StorageQuery.DisplayNameComparer);
        private readonly StorageIndex<PhysicalStoredItem> priceIndex = new(StorageQuery.PriceComparer);
        private readonly StorageIndex<PhysicalStoredItem> heatIndex = new(StorageQuery.HeatComparer);
        private readonly Dictionary<GameItem, PhysicalStoredItem> items = new();

        internal static MachineryPanel GetPanel(GameInventory inventory) =>
            (inventory as PixelElement)?.handler?.FindParentElement<MachineryPanel>();


        public InventoryStorageProvider(GameInventory inventory, string name = "Unknow")
        {
            this.inventory = inventory;
            this.name = name;
            
            this.inventory.onSlotAddItemEndFunc += onSlotAddItemEndFunc;
            this.inventory.onSlotRemoveItemEndFunc += onSlotRemoveItemEndFunc;
            this.inventory.onToggleSlotItemFunc += onToggleSlotItemFunc;
            foreach (var item in inventory.childItems) AddItem(item);
        }

        public InventoryStorageProvider(GameInventory inventory, GameItem nameSource)
            : this(inventory)
        {
            this.nameSource = nameSource;
        }

        public async IAsyncEnumerable<StoredItem> SearchAsync(StorageQuery query,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RefreshIndexes();
            var index = query.sort switch
            {
                StorageQuery.SortField.PRICE => priceIndex,
                StorageQuery.SortField.STOLEN => heatIndex,
                _ => displayNameIndex
            };
            int step = query.descending ? -1 : 1;
            int start = query.descending ? index.Count - 1 : 0;
            var timer = Stopwatch.StartNew();

            for (int i = start;
                 i >= 0 && i < index.Count;
                 i += step)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = index[i];

                if (index.MatchRequiredMask(i, query.requiredMask)
                    && (query.anyMask == 0 || index.MatchAnyMask(i, query.anyMask))
                    && index.MatchExcludedMask(i, query.excludedMask)
                    && query.matchPrice(item.Price)
                    && query.matchName(item.DisplayName))
                {
                    yield return item;
                    timer.Restart();
                }

                if (timer.Elapsed.TotalMilliseconds >= SearchBudgetMilliseconds)
                {
                    await Task.Yield();
                    timer.Restart();
                }
            }
        }

        public void Dispose()
        {
            inventory.onSlotAddItemEndFunc -= onSlotAddItemEndFunc;
            inventory.onSlotRemoveItemEndFunc -= onSlotRemoveItemEndFunc;
            inventory.onToggleSlotItemFunc -= onToggleSlotItemFunc;
        }

        private void  onSlotAddItemEndFunc(GameItem item, GameInventory inventory, SlotMarker slot)
        {
            AddItem(item);
        }

        private void onSlotRemoveItemEndFunc(GameItem item, GameInventory inventory, SlotMarker slot)
        {
            if (items.TryGetValue(item, out var storedItem))
            {
                displayNameIndex.Remove(storedItem);
                priceIndex.Remove(storedItem);
                heatIndex.Remove(storedItem);
                items.Remove(item);
            }
        }

        private void AddItem(GameItem item)
        {
            if (items.ContainsKey(item)) return;
            var storedItem = new PhysicalStoredItem(item);
            items.Add(item, storedItem);
            displayNameIndex.Add(storedItem, storedItem.TagMask);
            priceIndex.Add(storedItem, storedItem.TagMask);
            if (storedItem.Heat.HasValue) heatIndex.Add(storedItem, storedItem.TagMask);
        }

        private void RefreshIndexes(bool namesChanged = false)
        {
            bool pricesChanged = namesChanged;
            bool heatChanged = namesChanged;
            foreach (var storedItem in items.Values)
            {
                long oldPrice = storedItem.Price;
                int? oldHeat = storedItem.Heat;
                storedItem.Refresh();
                pricesChanged |= oldPrice != storedItem.Price;
                heatChanged |= oldHeat != storedItem.Heat;
                if (oldHeat.HasValue && !storedItem.Heat.HasValue) heatIndex.Remove(storedItem);
                else if (!oldHeat.HasValue && storedItem.Heat.HasValue)
                    heatIndex.Add(storedItem, storedItem.TagMask);
            }
            if (namesChanged) displayNameIndex.Refresh();
            if (pricesChanged) priceIndex.Refresh();
            if (heatChanged) heatIndex.Refresh();
            RefreshMasks(displayNameIndex);
            RefreshMasks(priceIndex);
            RefreshMasks(heatIndex);
        }

        private static void RefreshMasks(StorageIndex<PhysicalStoredItem> index)
        {
            for (int i = 0; i < index.Count; i++) index.SetMask(i, index[i].TagMask);
        }

        private void onToggleSlotItemFunc(GameItem item, GameInventory inventory, SlotMarker slot)
        {
            RefreshIndexes(namesChanged: true);
        }
    }
}
