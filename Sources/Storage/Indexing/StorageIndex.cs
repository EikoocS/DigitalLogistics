using System;
using System.Collections.Generic;
using DigitalLogistics.Collections;
using DigitalLogistics.Filtering;
using DigitalLogistics.Storage.Items;
using static DigitalLogistics.Storage.Indexing.StorageSort;

namespace DigitalLogistics.Storage.Indexing
{
    public class StorageIndex
    {
        private readonly Dictionary<int, CachedItem> UniqueIdIndex = new();
        private readonly Dictionary<SortBy, OrderList<CachedItem>> itemIndexs = new();
        private readonly Dictionary<SortBy, BloomFilter> indexBlooms = new();
        private readonly BloomFilter typeBloom = new();

        public int Size => UniqueIdIndex.Count;
        public event Action onChange;

        public StorageIndex()
        {
            foreach (SortBy sortBy in Enum.GetValues(typeof(SortBy)))
                itemIndexs[sortBy] = new OrderList<CachedItem>(sortBy.Comparer(), item => sortBy.Getter()(item) != null);
            foreach (var field in (SortBy[])Enum.GetValues(typeof(SortBy)))
                indexBlooms[field] = new ();
        }

        public bool Contains(int uniqueId) => UniqueIdIndex.ContainsKey(uniqueId);
        internal CachedItem Find(int uniqueId) => UniqueIdIndex.TryGetValue(uniqueId, out var item) ? item : null;

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

        internal bool MayMatch(StoredItemExpression expression) =>
            StoredItemExpressionAnalyzer.MayMatch(new[] { expression }, indexBlooms, typeBloom);

        public IEnumerator<CachedItem> GetEnumerator(SortBy sortBy, bool reverse)
        {
            var index = itemIndexs[sortBy];
            return index.GetEnumerator(reverse);
        }
    }
}
