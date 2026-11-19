using System;
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
            ItemSerialization.Inspect(entry.Item, entry.Count, item =>
            {
                CacheProperties(item);
                return 0;
            });
            UniqueId = entry.Id;
        }

        internal override T ReadItem<T>(Func<GameItem, T> read) => VerifyValidity()
            ? ItemSerialization.Inspect(entry.Item, entry.Count, read) : default;

        public override bool VerifyValidity() => provider.Contains(UniqueId);
        public override ItemInstanceLease AcquireInstance() => VerifyValidity()
            ? new(ItemSerialization.Decode(entry.Item, entry.Count), true) : null;
        public StorageOutput PrepareOutput() => provider.PrepareOutput(entry);
    }
}
