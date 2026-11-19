using System.Linq;
using DigitalLogistics.Storage.Disks;

namespace DigitalLogistics.Item
{
    internal sealed class FluidDisk : StorageDisk
    {
        internal const string ItemId = "fluid_disk";
        protected override string StorageType => "fluid";
        protected override string CapacityFormat => "#,0.###";
        protected override string CapacityUnit => " ml";
        internal int CapacityUL => Capacity * 1000;
        private FluidDisk() : base(ItemId) { }
        internal static GameItem Create() => new FluidDisk();

        protected override double GetUsedCapacity(StorageDiskData data) => data == null ? 0
            : Liquid.Liquids.Sum(liquid => data.Fluid.GetTag(liquid.currentPart).GetInt()) / 1000.0;

        protected override void InitializeStorage(StorageDiskData data)
        {
            data.Fluid.GetTag(WaterHelper.LIQUID_CONTAINER_CAPACITY).Enable().SetInt(CapacityUL);
            data.Fluid.GetTag(ChemicalHelper.CONTAINER_TEMP_TAG).Enable().SetDouble(24);
        }
    }
}
