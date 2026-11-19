using System;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Buses;
using DigitalLogistics.Item;
using DigitalLogistics.Storage.Providers;

namespace DigitalLogistics.Storage
{
    internal static class FluidProviderRegistry
    {
        internal sealed class Link
        {
            internal object Identity;
            internal string Key;
            internal string Name;
            internal FluidStorageProvider Provider;
            internal readonly List<GameItem> Owners = new();
        }

        private static readonly Dictionary<object, Link> links = new();

        internal static Link Acquire(GameItem item)
        {
            if (BusAccess.IsBlocked(item)) return null;
            var disk = item as FluidDisk;
            var data = disk?.GetStorage();
            if (disk != null && data == null) return null;
            object identity = disk == null ? item : disk.DiskId.Value;
            if (links.TryGetValue(identity, out var existing))
            {
                existing.Owners.Add(item);
                return existing;
            }
            var link = new Link
            {
                Identity = identity,
                Key = disk == null ? "fluid:" + item.GetUniqueID() : "fluid.disk:" + disk.DiskId.Value.ToString("D"),
                Name = disk == null ? item.GetDisplayName(preferCustomName: true) : disk.DiskName
            };
            link.Owners.Add(item);
            link.Provider = disk == null
                ? new ContainerStorageProvider(item, link.Name)
                : new FluidDiskStorageProvider(disk.DiskId.Value, data, disk,
                    () => link.Owners.Any(owner => !BusAccess.IsBlocked(owner)));
            links.Add(identity, link);
            return link;
        }

        internal static void Release(Link link, GameItem owner)
        {
            link.Owners.Remove(owner);
            if (link.Owners.Count != 0) return;
            links.Remove(link.Identity);
            link.Provider.Dispose();
        }

        internal static void Clear()
        {
            foreach (var link in links.Values) link.Provider.Dispose();
            links.Clear();
        }
    }
}
