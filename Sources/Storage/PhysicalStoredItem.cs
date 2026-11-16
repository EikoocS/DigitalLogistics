namespace DigitalStorage.Storage
{
    internal class PhysicalStoredItem : GameItemStoredItem
    {
        private readonly InventoryStorageProvider provider;

        public PhysicalStoredItem(GameItem item, InventoryStorageProvider provider) : base(item)
        {
            this.provider = provider;
        }

        public override bool VerifyValidity() =>
            !provider.Disposed && provider.Contains(item.uniqueId) && base.VerifyValidity();
    }
}
