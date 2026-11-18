namespace DigitalLogistics.Storage.Items
{
    internal class PhysicalItem : CachedItem
    {
        protected readonly GameItem item;

        internal PhysicalItem(GameItem item)
        {
            this.item = item;
            CacheProperties(item);
        }

        public override ItemInstanceLease AcquireInstance() => VerifyValidity() ? new(item) : null;
        public override bool VerifyValidity() => item.unitCount > 0;
    }
}
