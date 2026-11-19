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
        public int VolumeUL => Available ? ReadParts().Sum() : 0;
        public int CapacityUL => Available ? ReadTag(WaterHelper.LIQUID_CONTAINER_CAPACITY)?.GetInt() ?? 0 : 0;
        public int FreeCapacityUL => CapacityUL - VolumeUL;
        public double Temperature => ReadTag(ChemicalHelper.CONTAINER_TEMP_TAG)?.GetDouble() ?? 0;
        public virtual int[] ReadParts() => Liquid.Liquids.Select(liquid => ReadTag(liquid.currentPart)?.GetInt() ?? 0).ToArray();
        internal virtual GameItem PhysicalContainer => null;
        internal abstract void SetPart(int index, int volumeUL);
        internal abstract void ReceiveTemperature(int volumeUL, double temperature);
        internal abstract void Synchronize();
        internal virtual void Commit() => NotifyChanged();
    }
}
