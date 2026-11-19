using System;
using System.Linq;
using DigitalLogistics.Storage.Items;

namespace DigitalLogistics.Storage
{
    public abstract class FluidStorageProvider : StorageProvider
    {
        protected FluidStorageProvider(Func<bool> canAccess) : base(canAccess) { }
        public abstract CachedItem Content { get; }
        public abstract TagState ReadTag(string key);
        public long VolumeUL => Available ? ReadParts().Sum() : 0;
        public long CapacityUL => Available ? ReadVolumeTag(WaterHelper.LIQUID_CONTAINER_CAPACITY) : 0;
        public long FreeCapacityUL => CapacityUL - VolumeUL;
        public double Temperature => ReadTag(ChemicalHelper.CONTAINER_TEMP_TAG)?.GetDouble() ?? 0;
        public virtual long[] ReadParts() => Liquid.Liquids.Select(liquid => ReadVolumeTag(liquid.currentPart)).ToArray();
        internal virtual long ReadVolumeTag(string key) => ReadTag(key)?.GetInt() ?? 0;
        internal virtual GameItem PhysicalContainer => null;
        internal abstract void SetPart(int index, long volumeUL);
        internal abstract void ReceiveTemperature(int volumeUL, double temperature);
        internal abstract void Synchronize();
        internal virtual void Commit() => NotifyChanged();
    }
}
