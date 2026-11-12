using DigitalStorage.Storage;

namespace DigitalStorage.Storage
{
    internal class PhysicalStoredItem : StoredItem
    {
        private readonly GameItem item;
        private readonly InventoryStorageProvider provider;
        
        public PhysicalStoredItem(GameItem item, InventoryStorageProvider provider)
        {
            this.item = item;
            this.provider = provider;
            Refresh();
        }

        public GameItem Item => item;
        public bool IsAvailable => provider.Contains(item) && item.unitCount > 0;
        public string DisplayName => item.GetDisplayName(preferCustomName: true);
        public long Price { get; private set; }
        public int? Heat { get; private set; }
        public int ContrabandLevel { get; private set; }
        internal ulong TagMask { get; private set; }

        internal void Refresh()
        {
            Price = item.GetCurrentValue();
            Heat = StolenHelper.IsStolenItem(item) ? StolenHelper.GetFoundChance(item) : (int?)null;
            ContrabandLevel = ContrabandHelper.GetContrabandLevel(item);
            TagMask = StorageItemTypeHelper.FromTags(item.GetGameItemType());
            if (Heat.HasValue) TagMask |= (ulong)StorageItemType.STOLEN_TAG;
        }
    }
}
