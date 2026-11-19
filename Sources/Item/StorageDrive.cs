using System;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Buses;
using DigitalLogistics.Storage;
using UnityEngine;

namespace DigitalLogistics.Item
{
    internal sealed class StorageDrive : GameItemElement
    {
        internal const string SimpleId = "storage_drive_simple";
        internal const string ItemId = "storage_drive";
        internal const string ExpandedId = "storage_drive_expanded";
        private static readonly HashSet<StorageDrive> active = new();
        private readonly Vector2[] diskSlotOffsets;
        private readonly GameInventory inventory;
        private readonly Dictionary<StorageDisk, Action> disks = new();

        private StorageDrive(string id, int size, int columns, int rows, Vector2 firstSlot, Vector2 slotStep)
            : base(id, "Items/itematlas", "unknown")
        {
            diskSlotOffsets = new Vector2[columns * rows];
            for (int i = 0; i < diskSlotOffsets.Length; i++)
                diskSlotOffsets[i] = firstSlot + new Vector2(i % columns * slotStep.x, i / columns * slotStep.y);
            SetSpriteAndShapeFromMod(DigitalLogistics.Instance.Manifest.ID, id)
                .SetShape(new GridShapeBuilder().SetDataFill(size, size));
            var (window, contents) = DirectoryUtils.CreateInventoryWindow(columns * StorageDisk.SideInCells,
                rows * StorageDisk.SideInCells);
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
            onLoaded += _ =>
            {
                Connect();
                Validate();
            };
            inventory.onSlotAddItemEndFunc += OnDiskInventoryChanged;
            inventory.onSlotRemoveItemEndFunc += OnDiskInventoryChanged;
            getItemSpriteModifierForItemFunc = item => ((StorageDrive)item).GetDiskOverlays();
            Validate();
        }

        internal static void RegisterVariants(ModItemDirectory directory)
        {
            // Slot origins and spacing in artwork pixels; negative Y points down.
            directory.Add(SimpleId, () => new StorageDrive(SimpleId, 2, 2, 2, new(3, -14), new(14, -7)));
            directory.Add(ItemId, () => new StorageDrive(ItemId, 3, 2, 4, new(10, -21), new(16, -7)));
            directory.Add(ExpandedId, () => new StorageDrive(ExpandedId, 4, 4, 4, new(5, -27), new(14, -8)));
        }

        private static bool CanPlace(GameItem item, GameInventory target) => target.GetParentItem() == null;

        private List<ItemSpriteModifier> GetDiskOverlays()
        {
            var overlays = new List<ItemSpriteModifier>(diskSlotOffsets.Length * 3);
            int slot = 0;
            foreach (var disk in inventory.childItems.OfType<StorageDisk>().Take(diskSlotOffsets.Length))
                disk.AddDriveOverlays(overlays, diskSlotOffsets[slot++]);
            if (slot < diskSlotOffsets.Length)
            {
                var empty = ResolveSpriteByName("disk_overlay_null");
                for (; slot < diskSlotOffsets.Length; slot++)
                    overlays.Add(new ItemSpriteModifier(empty, diskSlotOffsets[slot]));
            }
            return overlays;
        }

        private void OnDiskInventoryChanged(GameItem _, GameInventory __, SlotMarker ___)
        {
            RefreshDisks();
            Validate();
        }

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
                TrackDisk(disk, provider, () =>
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
                TrackDisk(disk, link.Provider, () =>
                {
                    FluidProviderRegistry.Release(link, fluidDisk);
                    if (link.Owners.Count == 0) DigitalLogistics.Manager.Unregister(link.Provider);
                });
            }
        }

        private void TrackDisk(StorageDisk disk, StorageProvider provider, Action release)
        {
            provider.Changed += disk.RefreshCapacityStatus;
            disks.Add(disk, () =>
            {
                provider.Changed -= disk.RefreshCapacityStatus;
                release();
            });
            disk.RefreshCapacityStatus();
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
