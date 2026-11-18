using System.Collections.Generic;
using DigitalLogistics.Storage.Items;

namespace DigitalLogistics.Storage
{
    public sealed class StorageTransferResult
    {
        public int Amount { get; }
        public int SourceId { get; }
        public CachedItem RemainingSource { get; }
        public IReadOnlyCollection<CachedItem> Items { get; }
        public int? PlacedItemId { get; }

        internal StorageTransferResult(int amount, int sourceId, CachedItem remainingSource,
            IReadOnlyCollection<CachedItem> items, int? placedItemId)
        {
            Amount = amount;
            SourceId = sourceId;
            RemainingSource = remainingSource;
            Items = items;
            PlacedItemId = placedItemId;
        }
    }
}
