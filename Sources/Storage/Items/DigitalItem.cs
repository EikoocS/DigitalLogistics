using DigitalLogistics.Storage;
using DigitalLogistics.Storage.Disks;
using DigitalLogistics.Storage.Providers;

namespace DigitalLogistics.Storage.Items
{
    public sealed class DigitalItem : CachedItem, IStoredItem
    {
        private readonly ItemDiskStorageProvider provider;
        private readonly StorageDiskData.Entry entry;
        public ItemStorageProvider Provider => provider;

        internal DigitalItem(ItemDiskStorageProvider provider, StorageDiskData.Entry entry)
        {
            this.provider = provider;
            this.entry = entry;
            using var lease = new ItemInstanceLease(ItemSerialization.Decode(entry.Item, entry.Count), true);
            CacheProperties(lease.Item);
            UniqueId = entry.Id;
        }

        public override bool VerifyValidity() => provider.Contains(UniqueId);
        public override ItemInstanceLease AcquireInstance() => VerifyValidity()
            ? new(ItemSerialization.Decode(entry.Item, entry.Count), true) : null;
        public StorageOutput PrepareOutput() => provider.PrepareOutput(entry);
    }
}
