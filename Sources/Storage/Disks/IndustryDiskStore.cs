using System;
using DigitalLogistics.Item;

namespace DigitalLogistics.Storage.Disks
{
    internal static class IndustryDiskStore
    {
        [ThreadStatic] private static IndustryDisk snapshotRoot;
        [ThreadStatic] private static IndustryDisk restoringDisk;

        private static string Key(Guid id) => $"industry.disk.{id:D}";
        private static string ModId => DigitalLogistics.Instance.Manifest.ID;

        internal static bool IncludesContents(IndustryDisk disk) => ReferenceEquals(snapshotRoot, disk);

        internal static void Save(IndustryDisk disk)
        {
            string snapshot;
            snapshotRoot = disk;
            try { snapshot = ItemSerialization.EncodeSnapshot(disk); }
            finally { snapshotRoot = null; }

            string payload = DiskPayloadCodec.Encode(text => text.Write(snapshot), false, out _);
            ModHelper.SetModData(ModId, Key(disk.GetOrCreateDiskId()), payload);
        }

        internal static bool Restore(IndustryDisk disk)
        {
            if (disk.DiskId is not Guid id) return false;
            string payload = ModHelper.GetModData(ModId, Key(id))
                ?? throw new InvalidOperationException($"Missing industry disk snapshot {id:D}.");
            string snapshot = DiskPayloadCodec.Decode(payload, text => text.ReadToEnd());
            restoringDisk = disk;
            try
            {
                var carrier = ItemSerialization.DecodeSnapshot(snapshot, root => disk.TakeArea((IndustryDisk)root));
                carrier.uniqueId = 0;
                carrier.Destroy();
            }
            finally { restoringDisk = null; }
            return true;
        }

        internal static IndustryDisk TakeRestoringDisk()
        {
            var disk = restoringDisk;
            restoringDisk = null;
            return disk;
        }
    }
}
