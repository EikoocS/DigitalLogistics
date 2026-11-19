using System;
using DigitalLogistics.Item;
using DigitalLogistics.Storage.Disks;
using DigitalLogistics.Storage.Items;

namespace DigitalLogistics.Storage.Providers
{
    public sealed class FluidDiskStorageProvider : FluidStorageProvider
    {
        private readonly Guid diskId;
        private readonly StorageDiskData data;
        private readonly FluidDisk disk;
        public override CachedItem Content => new CachedFluidItem(this, disk);

        internal FluidDiskStorageProvider(Guid diskId, StorageDiskData data, FluidDisk disk, Func<bool> canAccess)
            : base(canAccess)
        {
            this.diskId = diskId;
            this.data = data;
            this.disk = disk;
            Name = disk.DiskName;
            Filter = disk.Filter;
        }

        public override TagState ReadTag(string key) => data.Fluid.GetTag(key).Clone();
        internal override void SetPart(int index, int volumeUL) =>
            data.Fluid.GetTag(Liquid.Liquids[index].currentPart).Enable().SetInt(volumeUL);
        internal override void ReceiveTemperature(int volumeUL, double temperature)
        {
            if (volumeUL <= 0) return;
            int existing = VolumeUL;
            data.Fluid.GetTag(ChemicalHelper.CONTAINER_TEMP_TAG).Enable().SetDouble(
                (existing * Temperature + volumeUL * temperature) / (existing + (double)volumeUL));
        }
        internal override void Synchronize() { }
        internal override void Commit()
        {
            StorageDiskStore.Set(diskId, data);
            base.Commit();
        }
        protected override void DisposeCore() { }
    }
}
