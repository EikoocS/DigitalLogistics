using DigitalLogistics.Storage;
using DigitalLogistics.Storage.Providers;

namespace DigitalLogistics.Storage.Items
{
    internal class StoredPhysicalItem : PhysicalItem, IStoredItem
    {
        private readonly InventoryStorageProvider provider;
        public ItemStorageProvider Provider => provider;

        public StoredPhysicalItem(GameItem item, InventoryStorageProvider provider) : base(item)
        {
            this.provider = provider;
        }

        public StorageOutput PrepareOutput() => VerifyValidity() ? new(AcquireInstance()) : null;

        public override bool VerifyValidity() =>
            !provider.Disposed && provider.Contains(item.uniqueId) && base.VerifyValidity();
    }
}
