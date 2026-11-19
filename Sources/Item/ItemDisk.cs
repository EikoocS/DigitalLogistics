using DigitalLogistics.Storage.Disks;

namespace DigitalLogistics.Item
{
    internal sealed class ItemDisk : StorageDisk
    {
        internal const string FamilyId = "item_disk";
        protected override string StorageType => "item";
        protected override double GetUsedCapacity(StorageDiskData data) => data?.UsedCells ?? 0;
        private ItemDisk(string id, string tier, int capacity) : base(id, FamilyId, tier, capacity) { }
        internal static GameItem Create(string id, string tier, int capacity) => new ItemDisk(id, tier, capacity);
    }
}
