using DigitalStorage.Collections;
using System;
using System.Collections.Generic;
using static DigitalStorage.Storage.StorageSort;

namespace DigitalStorage.Storage
{
    public class StorageIndex
    {
        public static readonly Comparer<CachedItem> IdentifierComparer = ComparerHelper.ComparedBy((CachedItem item) => item.Identifier);
        public static readonly Comparer<CachedItem> ModIdComparer = ComparerHelper.ComparedBy((CachedItem item) => item.ModId).LinkComparer(IdentifierComparer);
        public static readonly Comparer<CachedItem> DisplayNameComparer = ComparerHelper.ComparedBy((CachedItem item) => item.DisplayName).LinkComparer(IdentifierComparer);
        public static readonly Comparer<CachedItem> PriceComparer = ComparerHelper.ComparedBy((CachedItem item) => item.Price).LinkComparer(IdentifierComparer);
        public static readonly Comparer<CachedItem> BasePriceComparer = ComparerHelper.ComparedBy((CachedItem item) => item.BasePrice).LinkComparer(IdentifierComparer);
        public static readonly Comparer<CachedItem> HeatComparer = ComparerHelper.ComparedBy((CachedItem item) => item.Heat).LinkComparer(IdentifierComparer);
        private readonly Dictionary<int, CachedItem> UniqueIdIndex = new();
        private readonly Dictionary<SortBy, OrderList<CachedItem>> itemIndexs = new();
        private readonly Dictionary<SortBy, BloomFilter> indexBlooms = new();
        private readonly BloomFilter typeBloom = new();

        public int Size => UniqueIdIndex.Count;
        public event Action onChange;

        public StorageIndex()
        {
            foreach (SortBy sortBy in Enum.GetValues(typeof(SortBy)))
                itemIndexs[sortBy] = OrderList<CachedItem>.BuildByGetterAndFilter(sortBy.Getter());
            foreach (var field in (SortBy[])Enum.GetValues(typeof(SortBy)))
                indexBlooms[field] = new ();
        }

        public bool Contains(int uniqueId) => UniqueIdIndex.ContainsKey(uniqueId);

        public bool Add(CachedItem item)
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

            onChange?.Invoke();
            return true;
        }

        public bool Remove(CachedItem item)
        {
            if (!UniqueIdIndex.ContainsKey(item.UniqueId))
                return false;
            else
                UniqueIdIndex.Remove(item.UniqueId);

            foreach (SortBy sortBy in Enum.GetValues(typeof(SortBy)))
                lock (itemIndexs[sortBy]) 
                    itemIndexs[sortBy].Remove(item);

            onChange?.Invoke();
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

            foreach (var bloom in indexBlooms.Values) bloom.Clear();
            typeBloom.Clear();
            onChange?.Invoke();
        }

        public Dictionary<SortBy, BloomFilter> GetIndexBloom()
        {
            return indexBlooms;
        }

        public BloomFilter GetTypeBloom()
        {
            return typeBloom;
        }

        public IEnumerator<CachedItem> GetEnumerator(SortBy sortBy, bool reverse)
        {
            var index = itemIndexs[sortBy];
            return index.GetEnumerator(reverse);
        }
    }
}
