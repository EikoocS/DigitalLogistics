using DigitalLogistics.Storage.Disks;

namespace DigitalLogistics.Item
{
    internal sealed class ItemDisk : StorageDisk
    {
        internal const string ItemId = "item_disk";
        protected override string StorageType => "item";
        protected override double GetUsedCapacity(StorageDiskData data) => data?.UsedCells ?? 0;
        private ItemDisk() : base(ItemId) { }
        internal static GameItem Create() => new ItemDisk();
    }
}
