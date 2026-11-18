using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DigitalLogistics.Storage.Disks
{
    internal static class StorageDiskStore
    {
        private const string DirectoryKey = "storage.disk";
        private static readonly Dictionary<Guid, StorageDiskData> disks = new();
        private static readonly HashSet<Guid> dirtyDisks = new();
        private static HashSet<Guid> diskIds;
        private static bool directoryDirty;

        private static string ModId => DigitalLogistics.Instance.Manifest.ID;
        private static string DiskKey(Guid id) => $"{DirectoryKey}.{id:D}";
        private static HashSet<Guid> Directory => diskIds ??= LoadDirectory();

        internal static IEnumerable<Guid> DiskIds => Directory;

        private static HashSet<Guid> LoadDirectory()
        {
            string json = ModHelper.GetModData(ModId, DirectoryKey);
            return json == null ? new() : JsonConvert.DeserializeObject<HashSet<Guid>>(json);
        }

        internal static StorageDiskData Get(Guid id)
        {
            if (!Directory.Contains(id)) return null;
            if (!disks.TryGetValue(id, out var disk))
                disks[id] = disk = StorageDiskData.Decode(ModHelper.GetModData(ModId, DiskKey(id)));
            return disk;
        }

        /// <summary>Call after modifying a loaded disk, or to register a new disk.</summary>
        internal static void Set(Guid id, StorageDiskData disk)
        {
            if (Directory.Add(id)) directoryDirty = true;
            disks[id] = disk;
            dirtyDisks.Add(id);
        }

        internal static void Save()
        {
            foreach (Guid id in dirtyDisks)
                ModHelper.SetModData(ModId, DiskKey(id), disks[id].Encode());
            if (directoryDirty)
                ModHelper.SetModData(ModId, DirectoryKey, JsonConvert.SerializeObject(Directory));
            dirtyDisks.Clear();
            directoryDirty = false;
        }

        internal static void Clear()
        {
            disks.Clear();
            dirtyDisks.Clear();
            diskIds = null;
            directoryDirty = false;
        }
    }
}
