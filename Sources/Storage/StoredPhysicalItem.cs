namespace DigitalLogistics.Storage
{
    internal class StoredPhysicalItem : PhysicalItem, StoredItem
    {
        private readonly InventoryStorageProvider provider;
        public StorageProvider Provider => provider;

        public StoredPhysicalItem(GameItem item, InventoryStorageProvider provider) : base(item)
        {
            this.provider = provider;
        }

        public override bool VerifyValidity() =>
            !provider.Disposed && provider.Contains(item.uniqueId) && base.VerifyValidity();
    }
}
