using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Item;

namespace DigitalLogistics.Storage
{
    public sealed class DiskStorageProvider : StorageProvider
    {
        public static IList<Predicate<GameItem>> Blacklist { get; } = new List<Predicate<GameItem>>
        {
            item => item.IsTag("SYSTEM_TAG") && item.HaveType("STORAGE"),
            item => item.IsTag("ITEM_HIDDEN_TAG") && item.HaveType("STORAGE"),
            item => item.IsTag(ContainerHelper.BACKPACK_TAG) || item.IsTag("backpack"),
            item => item.IsTag(LouToyBoxHelper.TOY_BOX_TAG),
            item => item.IsTag(LockHelper.LOCKED_CONTAINER_TAG) || item.HaveType("SUPPLY_CRATE")
                || item.identifier == "luxury_supply_crate"
        };

        private readonly StorageDiskData data;
        private readonly Guid diskId;
        private readonly int capacity;
        private bool disposed;

        internal DiskStorageProvider(ItemDisk disk, StorageDiskData data)
        {
            this.data = data;
            diskId = disk.DiskId.Value;
            capacity = disk.Capacity;
            Name = disk.DiskName;
            filter = disk.Filter;
            Refresh();
        }

        public override bool Contains(int uniqueId) => !disposed && storageIndex.Contains(uniqueId);
        public override void Dispose() => disposed = true;

        public override void Refresh()
        {
            if (disposed) return;
            var index = new StorageIndex();
            foreach (var item in data.Items)
                index.Add(new DigitalItem(this, item));
            ReplaceIndex(index);
        }

        public override IEnumerator RefreshCoroutine(Action onItemProcessed)
        {
            if (disposed) yield break;
            Refresh();
            for (int i = 0; i < Size; i++) onItemProcessed();
        }

        private static bool IsBlocked(GameItem item) =>
            item is ItemDisk || Bus.IsBus(item) || Blacklist.Any(rule => rule(item));

        protected override StorageInputResult InputCore(GameItem item, int amount)
        {
            if (disposed || IsBlocked(item) || !GeneralHelper.IsItemOwned(item)
                || (item.parentInventory != null && !item.MayRemove())
                || GraphUtils.FindAllChildrenType<GameItem>(item).Any(IsBlocked))
                return StorageInputResult.Empty;

            int moved = Math.Min(amount, item.MaxNumRemove());
            if (moved <= 0) return StorageInputResult.Empty;

            bool hasChildren = GraphUtils.FindAllChildrenType<GameItem>(item).Count > 0;
            if (hasChildren && item.unitCount != 1) return StorageInputResult.Empty;
            string encoded = ItemSerialization.Encode(item);
            var existing = hasChildren ? null : FindStack(item, encoded);
            StorageDiskData.Entry entry;
            if (existing == null)
            {
                int cells = 0;
                var shape = item.modifiedShape;
                for (int y = 0; y < shape.height; y++)
                    for (int x = 0; x < shape.width; x++)
                        if (shape.GetLocal(x, y) == 1) cells++;
                if (cells <= 0 || cells > capacity - data.UsedCells)
                    return StorageInputResult.Empty;
                entry = new StorageDiskData.Entry
                {
                    Id = PlayerStore.Instance.GetUniqueID(),
                    Cells = cells,
                    Item = encoded
                };
            }
            else
            {
                entry = existing;
                moved = Math.Min(moved, int.MaxValue - entry.Count);
            }

            if (moved <= 0 || !RemoveFromSource(item, moved)) return StorageInputResult.Empty;
            if (existing == null) data.Items.Add(entry);
            entry.Count += moved;
            var stored = UpdateIndex(entry);
            return new StorageInputResult(moved, new[] { stored });
        }

        private StorageDiskData.Entry FindStack(GameItem item, string encoded)
        {
            if (item.mayStackItemFunc == null) return null;
            foreach (var candidate in data.Items)
            {
                if (candidate.Count == int.MaxValue || candidate.Item != encoded) continue;
                var target = ItemSerialization.Decode(candidate.Item, candidate.Count);
                try { if (target.MayStack(item)) return candidate; }
                finally { target.Destroy(); }
            }
            return null;
        }

        private static bool RemoveFromSource(GameItem item, int count)
        {
            var inventory = item.parentInventory;
            if (count == item.unitCount)
            {
                if (inventory != null && !inventory.Expel(item)) return false;
                item.unitCount = 0;
                var drag = ItemMouseDragHandler.current;
                if (drag == null || !drag.IsDraggingItem || !ReferenceEquals(drag.GetCurrentItem(), item))
                    item.Destroy();
                return true;
            }

            item.unitCount -= count;
            item.Validate();
            item.onChangeAmountItemFunc?.Invoke(item);
            if (inventory != null)
            {
                var slot = inventory.GetIncompleteItemInventorySlot(item);
                item.onSlotChangeAmountItemFunc?.Invoke(item, inventory, slot);
                inventory.onSlotChangeAmountItemFunc?.Invoke(item, inventory, slot);
                OverlayHandler.current.UpdateInventoryStateUI();
            }
            return true;
        }

        internal StorageOutput PrepareOutput(StorageDiskData.Entry entry)
        {
            if (!Contains(entry.Id)) return null;
            var item = ItemSerialization.Decode(entry.Item, entry.Count);
            return new StorageOutput(item,
                moved =>
                {
                    entry.Count -= moved;
                    if (entry.Count == 0) data.Items.Remove(entry);
                    UpdateIndex(entry);
                },
                () => { if (item.parentInventory == null) item.Destroy(); },
                wholePlacement: true);
        }

        private CachedItem UpdateIndex(StorageDiskData.Entry entry)
        {
            StorageDiskStore.Set(diskId, data);
            storageIndex.Remove(entry.Id);
            if (entry.Count == 0) return null;
            var stored = new DigitalItem(this, entry);
            storageIndex.Add(stored);
            return stored;
        }
    }
}
