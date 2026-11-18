using System;

namespace DigitalLogistics.Storage
{
    public sealed class DigitalItem : CachedItem, StoredItem
    {
        private readonly DiskStorageProvider provider;
        private readonly StorageDiskData.Entry entry;
        public StorageProvider Provider => provider;

        internal DigitalItem(DiskStorageProvider provider, StorageDiskData.Entry entry)
        {
            this.provider = provider;
            this.entry = entry;
            var item = ItemSerialization.Decode(entry.Item, entry.Count);
            try { CacheProperties(item); }
            finally { item.Destroy(); }
            UniqueId = entry.Id;
        }

        public override bool IsDigitalItems() => true;
        public override bool VerifyValidity() => provider.Contains(UniqueId);
        public override GameItem GetItemInstance() =>
            VerifyValidity() ? ItemSerialization.Decode(entry.Item, entry.Count) : null;
        public override StorageOutput PrepareOutput() => provider.PrepareOutput(entry);
        public override TagState getTag(string key) => ReadItem(item =>
            item.modifiedState.dict.TryGetValue(key, out var tag) ? tag?.Clone() : null);

        public override RichText GetTooltip() => ReadItem(item =>
            item is TooltipListener listener && listener.HasTooltip()
                ? listener.GetTooltip() : new RichTextBuilder().Build());

        public override RichText GetTooltipAdvanced() => ReadItem(item =>
            item is TooltipListener listener && listener.HasTooltip()
                ? listener.GetTooltipAdvanced() : new RichTextBuilder().Build());

        private T ReadItem<T>(Func<GameItem, T> read)
        {
            var item = GetItemInstance();
            if (item == null) return default;
            try { return read(item); }
            finally { item.Destroy(); }
        }
    }
}
