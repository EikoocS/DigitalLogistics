using System.Linq;
using DigitalLogistics.Storage.Disks;

namespace DigitalLogistics.Item
{
    internal sealed class FluidDisk : StorageDisk
    {
        internal const string FamilyId = "fluid_disk";
        private const long MicrolitersPerLiter = 1_000_000;
        protected override string StorageType => "fluid";
        protected override string CapacityFormat => "#,0.######";
        protected override string CapacityUnit => " L";
        private FluidDisk(string id, string tier, int capacity) : base(id, FamilyId, tier, capacity) { }
        internal static GameItem Create(string id, string tier, int capacity) => new FluidDisk(id, tier, capacity);

        protected override double GetUsedCapacity(StorageDiskData data) => data == null ? 0
            : Liquid.Liquids.Sum(liquid => data.Fluid.GetTag(liquid.currentPart).GetLong()) / (double)MicrolitersPerLiter;

        protected override void InitializeStorage(StorageDiskData data)
        {
            data.Fluid.GetTag(WaterHelper.LIQUID_CONTAINER_CAPACITY).Enable().SetLong(Capacity * MicrolitersPerLiter);
            data.Fluid.GetTag(ChemicalHelper.CONTAINER_TEMP_TAG).Enable().SetDouble(24);
        }
    }
}
