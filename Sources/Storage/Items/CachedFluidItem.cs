namespace DigitalLogistics.Storage.Items
{
    internal sealed class CachedFluidItem : CachedItem
    {
        private readonly FluidStorageProvider provider;

        internal CachedFluidItem(FluidStorageProvider provider, GameItem disk)
        {
            this.provider = provider;
            CacheProperties(disk);
        }

        public override TagState GetTag(string key) => provider.ReadTag(key);
        public override int LiquidVolume => provider.VolumeUL;
        public override int FreeLiquidVolume => provider.FreeCapacityUL;
        public override bool VerifyValidity() => provider.Available;
        public override ItemInstanceLease AcquireInstance() => null;
    }
}
