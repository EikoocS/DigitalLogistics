using System;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Filtering;
using DigitalLogistics.Storage;
using DigitalLogistics.Storage.Disks;
using DigitalLogistics.Storage.Items;

namespace DigitalLogistics.Storage.Providers
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

        internal DiskStorageProvider(Guid diskId, StorageDiskData data, int capacity, string name,
            StoredItemExpression filter, Func<bool> canAccess) : base(canAccess)
        {
            this.diskId = diskId;
            this.data = data;
            this.capacity = capacity;
            Name = name;
            Filter = filter;
            Refresh();
        }

        public override bool Contains(int uniqueId) => FindIndexedItem(uniqueId) != null;
        protected override void DisposeCore() { }

        protected override IEnumerable<CachedItem> ReadItems()
        {
            if (!Available) yield break;
            foreach (var entry in data.Items.ToArray())
            {
                if (!Available) yield break;
                if (entry.Count > 0)
                    yield return new DigitalItem(this, entry);
            }
        }

        private static bool IsBlocked(GameItem item) => Blacklist.Any(rule => rule(item));

        protected override StorageInputResult InputCore(StorageOutput output, int amount)
        {
            var item = output.Item;
            if (Disposed || IsBlocked(item) || !GeneralHelper.IsItemOwned(item)
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

            if (moved <= 0 || !output.Consume(moved)) return StorageInputResult.Empty;
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

        internal StorageOutput PrepareOutput(StorageDiskData.Entry entry)
        {
            if (!Contains(entry.Id)) return null;
            return new StorageOutput(new ItemInstanceLease(ItemSerialization.Decode(entry.Item, entry.Count), true),
                moved =>
                {
                    entry.Count -= moved;
                    if (entry.Count == 0) data.Items.Remove(entry);
                    UpdateIndex(entry);
                });
        }

        private CachedItem UpdateIndex(StorageDiskData.Entry entry)
        {
            StorageDiskStore.Set(diskId, data);
            if (entry.Count == 0)
            {
                Remove(entry.Id);
                return null;
            }
            var stored = new DigitalItem(this, entry);
            Upsert(stored);
            return stored;
        }
    }
}
