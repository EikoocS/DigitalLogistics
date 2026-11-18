namespace DigitalLogistics.Storage
{
    internal class PhysicalItem : CachedItem
    {
        protected readonly GameItem item;

        public PhysicalItem(GameItem item)
        {
            this.item = item;
            CacheProperties(item);
        }

        public override bool IsDigitalItems() => false;

        public override GameItem GetItemInstance() => item;

        public override StorageOutput PrepareOutput() =>
            VerifyValidity() ? new StorageOutput(item) : null;

        public override bool VerifyValidity() => item.unitCount > 0;

        public override RichText GetTooltip() =>
            item is TooltipListener listener && listener.HasTooltip()
                ? listener.GetTooltip()
                : new RichTextBuilder().Build();

        public override RichText GetTooltipAdvanced() =>
            item is TooltipListener listener && listener.HasTooltip()
                ? listener.GetTooltipAdvanced()
                : new RichTextBuilder().Build();

        public override TagState getTag(string key) =>
            item.modifiedState.dict.TryGetValue(key, out var tag) ? tag?.Clone() : null;
    }
}
