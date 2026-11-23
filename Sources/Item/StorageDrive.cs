using System;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Storage;
using DigitalLogistics.Storage.Disks;
using DigitalLogistics.Tracing;
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
        private IEnumerable<IndustryDisk> IndustryDisks => inventory.childItems.OfType<IndustryDisk>();
        internal static event Action IndustryDisksChanged;

        internal static IEnumerable<IndustryDisk> MountedIndustryDisks() =>
            active.SelectMany(drive => drive.IndustryDisks.Where(drive.IsMounted));

        internal bool IsMounted(IndustryDisk disk) => active.Contains(this)
            && disk.parentInventory == inventory && disk.IsMaterialized && !ItemAccess.IsBlocked(disk);

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
            inventory.mayInventoryAddItemFunc = (item, target) => item is StorageDisk or IndustryDisk && canAdd(item, target);
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
            foreach (var disk in inventory.childItems.Where(item => item is StorageDisk or IndustryDisk)
                         .Take(diskSlotOffsets.Length))
            {
                var offset = diskSlotOffsets[slot++];
                if (disk is StorageDisk storageDisk) storageDisk.AddDriveOverlays(overlays, offset);
                else overlays.Add(new ItemSpriteModifier(ResolveSpriteByName("disk_overlay_item"), offset));
            }
            if (slot < diskSlotOffsets.Length)
            {
                var empty = ResolveSpriteByName("disk_overlay_null");
                for (; slot < diskSlotOffsets.Length; slot++)
                    overlays.Add(new ItemSpriteModifier(empty, diskSlotOffsets[slot]));
            }
            return overlays;
        }

        private void OnDiskInventoryChanged(GameItem item, GameInventory _, SlotMarker __)
        {
            if (item is IndustryDisk)
            {
                if (active.Contains(this))
                {
                    RefreshIndustryDisks();
                    IndustryDisksChanged?.Invoke();
                }
            }
            else RefreshDisks();
            Validate();
        }

        private void Connect()
        {
            if (!DigitalLogistics.Instance.Enabled || SaveManager.IsDecoding || parentInventory == null) return;
            var trace = PerformanceTrace.Active;
            var timer = new TraceTimer(trace != null);
            try
            {
                bool connected = active.Add(this);
                RefreshDisks();
                RefreshIndustryDisks();
                if (connected && IndustryDisks.Any()) IndustryDisksChanged?.Invoke();
            }
            finally { trace?.RecordDriveConnection(timer.ElapsedTicks); }
        }

        private void RefreshDisks()
        {
            if (SaveManager.IsDecoding || !active.Contains(this)) return;
            var desired = inventory.childItems.OfType<StorageDisk>().ToHashSet();
            foreach (var disk in disks.Keys.Except(desired).ToArray()) UnmountDisk(disk);
            foreach (var disk in desired)
                if (!disks.ContainsKey(disk)) MountDisk(disk);
        }

        private void RefreshIndustryDisks()
        {
            if (LogisticsSession.Loading || SaveManager.IsDecoding) return;
            foreach (var disk in IndustryDisks)
                if (!ItemAccess.IsBlocked(disk)) disk.Mount();
        }

        internal static void RestoreIndustryDisks()
        {
            foreach (var drive in active) drive.RefreshIndustryDisks();
            IndustryDisksChanged?.Invoke();
        }

        internal static void SaveIndustryDisks()
        {
            foreach (var disk in active.SelectMany(drive => drive.IndustryDisks))
                if (disk.IsMaterialized) IndustryDiskStore.Save(disk);
        }

        private void MountDisk(StorageDisk disk)
        {
            if (ItemAccess.IsBlocked(disk)) return;
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

        private void Disconnect(bool saveIndustry = true)
        {
            bool connected = active.Contains(this);
            var industryDisks = connected ? IndustryDisks.ToArray() : Array.Empty<IndustryDisk>();
            foreach (var disk in industryDisks) disk.Unmount(saveIndustry);
            active.Remove(this);
            foreach (var disk in disks.Keys.ToArray()) UnmountDisk(disk);
            if (industryDisks.Length != 0) IndustryDisksChanged?.Invoke();
        }

        internal static void ClearConnections(bool saveIndustry)
        {
            foreach (var drive in active.ToArray()) drive.Disconnect(saveIndustry);
        }
    }
}
