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
        private readonly StorageIndexBloom indexBloom = new();
        private readonly BloomFilter typeBloom = new();

        public StorageIndex()
        {
            foreach (SortBy sortBy in Enum.GetValues(typeof(SortBy)))
                itemIndexs[sortBy] = OrderList<StoredItem>.BuildByGetterAndFilter(sortBy.Getter());
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

            indexBloom.Add(item);
            typeBloom.Add(item.TypeMask.GetHashCode());

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

            indexBloom.Clear();
            typeBloom.Clear();
        }

        public StorageIndexBloom GetIndexBloom()
        {
            return indexBloom;
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
