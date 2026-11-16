using DigitalStorage.Collections;
using System;
using System.Collections.Generic;
using static DigitalStorage.Storage.StorageSort;

namespace DigitalStorage.Storage
{
    public class StorageIndex
    {
        public static readonly Comparer<StoredItem> IdentifierComparer = ComparerHelper.ComparedBy((StoredItem item) => item.Identifier);
        public static readonly Comparer<StoredItem> ModIdComparer = ComparerHelper.ComparedBy((StoredItem item) => item.ModId).LinkComparer(IdentifierComparer);
        public static readonly Comparer<StoredItem> DisplayNameComparer = ComparerHelper.ComparedBy((StoredItem item) => item.DisplayName).LinkComparer(IdentifierComparer);
        public static readonly Comparer<StoredItem> PriceComparer = ComparerHelper.ComparedBy((StoredItem item) => item.Price).LinkComparer(IdentifierComparer);
        public static readonly Comparer<StoredItem> BasePriceComparer = ComparerHelper.ComparedBy((StoredItem item) => item.BasePrice).LinkComparer(IdentifierComparer);
        public static readonly Comparer<StoredItem> HeatComparer = ComparerHelper.ComparedBy((StoredItem item) => item.Heat).LinkComparer(IdentifierComparer);
        private readonly Dictionary<int, StoredItem> UniqueIdIndex = new();
        private readonly Dictionary<SortBy, OrderList<StoredItem>> itemIndexs = new();
        private readonly Dictionary<SortBy, BloomFilter> indexBlooms = new();
        private readonly BloomFilter typeBloom = new();

        public StorageIndex()
        {
            foreach (SortBy sortBy in Enum.GetValues(typeof(SortBy)))
                itemIndexs[sortBy] = OrderList<StoredItem>.BuildByGetterAndFilter(sortBy.Getter());
            foreach (var field in (SortBy[])Enum.GetValues(typeof(SortBy)))
                indexBlooms[field] = new ();
        }

        public bool Contains(int uniqueId) => UniqueIdIndex.ContainsKey(uniqueId);

        public bool Add(StoredItem item)
        {
            if (UniqueIdIndex.ContainsKey(item.UniqueId))
                return false;
            else
                UniqueIdIndex.Add(item.UniqueId, item);

            foreach (SortBy sortBy in Enum.GetValues(typeof(SortBy)))
                lock (itemIndexs[sortBy]) 
                    itemIndexs[sortBy].Add(item);

            foreach (var entry in indexBlooms)
            {
                object value = entry.Key == SortBy.HEAT ? item.Heat ?? 0 : entry.Key.Getter()(item);
                entry.Value.Add(value?.GetHashCode() ?? 0);
            }
            for (int index = 0; index < item.TypeMask.Capacity; index++)
                if (item.TypeMask[index]) typeBloom.Add(index.GetHashCode());

            return true;
        }

        public bool Remove(StoredItem item)
        {
            if (!UniqueIdIndex.ContainsKey(item.UniqueId))
                return false;
            else
                UniqueIdIndex.Remove(item.UniqueId);

            foreach (SortBy sortBy in Enum.GetValues(typeof(SortBy)))
                lock (itemIndexs[sortBy]) 
                    itemIndexs[sortBy].Remove(item);

            return true;
        }

        public bool Remove(int uniqueId)
        {
            if (!UniqueIdIndex.ContainsKey(uniqueId))
                return false;
            else
                return Remove(UniqueIdIndex[uniqueId]);
        }

        public void Clear()
        {
            UniqueIdIndex.Clear();

            foreach (SortBy sortBy in Enum.GetValues(typeof(SortBy)))
                lock (itemIndexs[sortBy]) 
                    itemIndexs[sortBy].Clear();

            indexBlooms.Clear();
            typeBloom.Clear();
        }

        public Dictionary<SortBy, BloomFilter> GetIndexBloom()
        {
            return indexBlooms;
        }

        public BloomFilter GetTypeBloom()
        {
            return typeBloom;
        }

        public IEnumerator<StoredItem> GetEnumerator(SortBy sortBy, bool reverse)
        {
            var index = itemIndexs[sortBy];
            return index.GetEnumerator(reverse);
        }
    }
}
