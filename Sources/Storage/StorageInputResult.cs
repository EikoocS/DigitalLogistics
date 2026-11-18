using System;
using System.Collections.Generic;
using DigitalLogistics.Storage.Items;

namespace DigitalLogistics.Storage
{
    public sealed class StorageInputResult
    {
        public static StorageInputResult Empty { get; } = new(0, Array.Empty<CachedItem>());

        public int Amount { get; }
        public IReadOnlyCollection<CachedItem> Items { get; }

        public StorageInputResult(int amount, IReadOnlyCollection<CachedItem> items)
        {
            Amount = amount;
            Items = items;
        }
    }
}
