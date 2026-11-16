using System;
using System.Collections.Generic;

namespace DigitalStorage.Storage
{
    public sealed class StorageInputResult
    {
        public static StorageInputResult Empty { get; } = new(0, Array.Empty<StoredItem>());

        public int Amount { get; }
        public IReadOnlyCollection<StoredItem> Items { get; }

        public StorageInputResult(int amount, IReadOnlyCollection<StoredItem> items)
        {
            Amount = amount;
            Items = items;
        }
    }
}
