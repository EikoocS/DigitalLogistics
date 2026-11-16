using System;
using System.Collections.Generic;
using DigitalStorage.Collections;
using static DigitalStorage.Storage.StorageSort;

namespace DigitalStorage.Storage
{
    public sealed class StorageIndexBloom
    {
        private readonly Dictionary<SortBy, BloomFilter> blooms = new();

        public IReadOnlyDictionary<SortBy, BloomFilter> Blooms => blooms;
        public BloomFilter TypeBloom { get; }

        public StorageIndexBloom(int expectedCount = 1024, double falsePositiveProbability = 0.01)
            : this((SortBy[])Enum.GetValues(typeof(SortBy)), expectedCount, falsePositiveProbability)
        {
        }

        public StorageIndexBloom(IEnumerable<SortBy> fields, int expectedCount = 1024,
            double falsePositiveProbability = 0.01)
        {
            foreach (var field in fields)
                blooms[field] = new BloomFilter(expectedCount, falsePositiveProbability);
            TypeBloom = new BloomFilter(expectedCount, falsePositiveProbability);
        }

        public void Add(StoredItem item)
        {
            foreach (var entry in blooms)
            {
                object value = entry.Key == SortBy.HEAT ? item.Heat ?? 0 : entry.Key.Getter()(item);
                entry.Value.Add(value?.GetHashCode() ?? 0);
            }
            for (int index = 0; index < item.TypeMask.Capacity; index++)
                if (item.TypeMask[index]) TypeBloom.Add(index.GetHashCode());
        }

        /// <summary>Rebuilds all filters from current items; use after removals to discard stale hashes.</summary>
        public void Rebuild(IEnumerable<StoredItem> items)
        {
            Clear();
            foreach (var item in items) Add(item);
        }

        public void Clear()
        {
            foreach (var bloom in blooms.Values) bloom.Clear();
            TypeBloom.Clear();
        }
    }
}
