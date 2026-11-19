using System;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Buses;
using DigitalLogistics.Storage;

namespace DigitalLogistics.Item
{
    internal sealed class StorageDrive : GameItemElement
    {
        internal const string SimpleId = "storage_drive_simple";
        internal const string ItemId = "storage_drive";
        internal const string ExpandedId = "storage_drive_expanded";
        private static readonly HashSet<StorageDrive> active = new();
        private readonly GameInventory inventory;
        private readonly Dictionary<StorageDisk, Action> disks = new();

        private StorageDrive(string id, int size, int width, int height)
            : base(id, "Items/itematlas", "unknown")
        {
            SetSpriteAndShapeFromMod(DigitalLogistics.Instance.Manifest.ID, id)
                .SetShape(new GridShapeBuilder().SetDataFill(size, size));
            var (window, contents) = DirectoryUtils.CreateInventoryWindow(width, height);
            inventory = contents;
            SetContentWindow(window);
            name = DigitalLogistics.Localize($"item.{id}.name");
            shortDescription = DigitalLogistics.Localize($"item.{id}.desc");
            flavorText = DigitalLogistics.Localize($"item.{id}.flavor");
            SetGameItemType("STORAGE");
            EnableTag(ShipHelper.SYSTEM_TAG);
            EnableTag(ShipHelper.SYSTEM_TAG_UTILITY);
            ContainerHelper.InitContainerItem(inventory, this);
            inventory.EnableTag(HopperHelper.NO_HOPPER_INVENTORY_TAG);
            var canAdd = inventory.mayInventoryAddItemFunc;
            inventory.mayInventoryAddItemFunc = (item, target) => item is StorageDisk && canAdd(item, target);
            mayInventoryAddItemFunc = CanPlace;
            onSlotAddItemFunc += (_, _, _) => Connect();
            onSlotRemoveItemFunc += (_, _, _) => Disconnect();
            onLoaded += _ => Connect();
            inventory.onSlotAddItemEndFunc += (_, _, _) => RefreshDisks();
            inventory.onSlotRemoveItemEndFunc += (_, _, _) => RefreshDisks();
        }

        internal static GameItem Create(string id, int size, int width, int height) =>
            new StorageDrive(id, size, width, height);

        private static bool CanPlace(GameItem item, GameInventory target) => target.GetParentItem() == null;

        private void Connect()
        {
            if (!DigitalLogistics.Instance.Enabled || SaveManager.IsDecoding || parentInventory == null) return;
            active.Add(this);
            RefreshDisks();
        }

        private void RefreshDisks()
        {
            if (SaveManager.IsDecoding || !active.Contains(this)) return;
            var desired = inventory.childItems.OfType<StorageDisk>().ToHashSet();
            foreach (var disk in disks.Keys.Except(desired).ToArray()) UnmountDisk(disk);
            foreach (var disk in desired)
                if (!disks.ContainsKey(disk)) MountDisk(disk);
        }

        private void MountDisk(StorageDisk disk)
        {
            if (BusAccess.IsBlocked(disk)) return;
            if (disk is ItemDisk itemDisk)
            {
                var data = itemDisk.GetStorage();
                if (data == null) return;
                var id = itemDisk.DiskId.Value;
                var provider = SharedProviderRegistry.AcquireDisk(id, data, itemDisk.Capacity,
                    itemDisk.DiskName, itemDisk.Filter, itemDisk);
                DigitalLogistics.Manager.Register(provider);
                disks.Add(disk, () =>
                {
                    var released = SharedProviderRegistry.ReleaseDisk(id, itemDisk);
                    if (released != null) DigitalLogistics.Manager.Unregister(released);
                });
            }
            else if (disk is FluidDisk fluidDisk)
            {
                var link = FluidProviderRegistry.Acquire(fluidDisk);
                if (link == null) return;
                DigitalLogistics.Manager.Register(link.Provider);
                disks.Add(disk, () =>
                {
                    FluidProviderRegistry.Release(link, fluidDisk);
                    if (link.Owners.Count == 0) DigitalLogistics.Manager.Unregister(link.Provider);
                });
            }
        }

        private void UnmountDisk(StorageDisk disk)
        {
            disks[disk]();
            disks.Remove(disk);
        }

        private void Disconnect()
        {
            active.Remove(this);
            foreach (var disk in disks.Keys.ToArray()) UnmountDisk(disk);
        }

        internal static void ClearConnections()
        {
            foreach (var drive in active.ToArray()) drive.Disconnect();
        }
    }
}
