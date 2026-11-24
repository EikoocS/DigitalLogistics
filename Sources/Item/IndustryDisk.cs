using System;
using System.Collections.Generic;
using DigitalLogistics.Patch;
using DigitalLogistics.Storage.Disks;
using DigitalLogistics.UI;

namespace DigitalLogistics.Item
{
    internal sealed class IndustryDisk : GameItemElement
    {
        internal const string ItemId = "industry_disk";
        internal const string IngotId = "metal_ingot";
        internal const int MaxSide = 20;
        private const int InitialSide = 6;
        private const int BasePrice = 400;
        private const int IngotBasePrice = 50;
        private const string DiskIdTag = "DIGITAL_LOGISTICS_INDUSTRY_DISK_UUID_STRING";
        private const string ColumnsTag = "DIGITAL_LOGISTICS_INDUSTRY_COLUMNS";
        private const string RowsTag = "DIGITAL_LOGISTICS_INDUSTRY_ROWS";

        private IndustryAreaUI ui;
        internal Guid? DiskId => state.dict.TryGetValue(DiskIdTag, out var tag)
            && Guid.TryParse(tag.GetString(), out var id) ? id : null;
        internal bool IsMaterialized => ui != null;
        internal int Columns => ui.Area.inventoryShape.width;
        internal int Rows => ui.Area.inventoryShape.height;
        internal bool CanOpen => IsMaterialized
            && parentInventory?.GetParentItem() is StorageDrive drive && drive.IsMounted(this);

        public override List<GraphNodeStorage> children =>
            ui == null ? base.children
                : StorageTreeEncoding.Active && !IndustryDiskStore.IncludesContents(this) ? new()
                : new() { ui.AreaWindow, ui.UpgradeWindow };

        private IndustryDisk() : base(ItemId, "Items/itematlas", "unknown")
        {
            unitValue = BasePrice;
            SetSpriteAndShapeFromMod(DigitalLogistics.Instance.Manifest.ID, ItemId)
                .SetShape(new GridShapeBuilder().SetDataFill(3, 2));
            name = DigitalLogistics.Localize("item.industry_disk.name");
            shortDescription = DigitalLogistics.Localize("item.industry_disk.desc");
            flavorText = DigitalLogistics.Localize("item.industry_disk.flavor");
            SetGameItemType("STORAGE");
            ContainerHelper.InitContainerItemTagOnly(this);
            EnableTag(ContainerHelper.NESTABLE_DEVICE_TAG);
            EnableTag(GeneralHelper.IS_UNLOAD_DISABLED_TAG);
            mayStackItemFunc = (_, _) => false;
            onSlotRemoveItemFunc += (_, _, _) => Unmount(save: true);
            onLoaded += _ => RefreshValue();
        }

        private IndustryAreaUI CreateArea(int columns, int rows)
        {
            var area = new GameGridInventory(columns, rows).SetMachineryStyle();
            area.mayInventoryAddItemFunc = (item, inventory) => GeneralHelper.IsItemOwned(item)
                && inventory.GetParentItem() is IndustryDisk owner && GeneralHelper.IsItemOwned(owner)
                && !item.IsTag(ContainerHelper.BACKPACK_TAG)
                && (!item.IsTag("STORAGE") || ContainerHelper.IsNestingAllowed(item, owner));

            var upgradeSlot = new GameSlotInventory().SetMachineryStyle();
            upgradeSlot.EnableTag(HopperHelper.NO_HOPPER_INVENTORY_TAG);
            upgradeSlot.mayInventoryAddItemFunc = (item, inventory) => item.identifier == IngotId
                && GeneralHelper.IsItemOwned(item) && GeneralHelper.IsItemOwned(inventory.GetParentItem());
            return new IndustryAreaUI(this, area, upgradeSlot);
        }

        private void AttachArea(IndustryAreaUI area)
        {
            ui = area;
            ui.Bind(this);
            SetContentWindow(ui.AreaWindow);
            ui.UpgradeWindow.AddParent(this);
        }

        internal void TakeArea(IndustryDisk carrier)
        {
            var area = carrier.ui;
            carrier.SetContentWindow(null);
            area.UpgradeWindow.RemoveParent(carrier);
            carrier.ui = null;
            AttachArea(area);
        }

        internal static GameItem Create()
        {
            var disk = new IndustryDisk();
            if (IndustryDiskStore.TakeRestoringDisk() is { } source)
                disk.AttachArea(disk.CreateArea(source.ReadSide(ColumnsTag), source.ReadSide(RowsTag)));
            return disk;
        }

        internal Guid GetOrCreateDiskId()
        {
            if (DiskId is Guid id) return id;
            id = Guid.NewGuid();
            ModifyTag(DiskIdTag, tag => tag.Enable().SetString(id.ToString("D")), skipValidate: true);
            return id;
        }

        internal void Mount()
        {
            if (IsMaterialized) return;
            try
            {
                if (!IndustryDiskStore.Restore(this))
                    AttachArea(CreateArea(ReadSide(ColumnsTag), ReadSide(RowsTag)));
            }
            catch (Exception error)
            {
                if (IsMaterialized) DisposeArea();
                DigitalLogistics.Instance.Log.Error($"Failed to mount industry disk {DiskId}: {error}");
            }
        }

        internal void Unmount(bool save)
        {
            if (!IsMaterialized) return;
            if (save)
            {
                IndustryDiskStore.Save(this);
                foreach (var hopper in GraphUtils.FindAllChildrenType<Hopper>(this))
                    hopper.Configuration.Save(hopper);
            }
            DisposeArea();
        }

        internal void OpenArea()
        {
            if (CanOpen) ui.Open();
        }

        internal static string RequiredPurity(int nextSize) => nextSize switch
        {
            <= 8 => IngotPurityHelper.INGOT_PURITY_FAIR,
            <= 11 => IngotPurityHelper.INGOT_PURITY_HIGH,
            <= 13 => IngotPurityHelper.INGOT_PURITY_VERY_HIGH,
            _ => IngotPurityHelper.INGOT_PURITY_PERFECT
        };

        private static double RequiredIngotValue(int side)
        {
            double total = 0;
            for (int size = InitialSide + 1; size <= side; size++)
                total += IngotBasePrice * (1 + IngotPurityHelper.ingotPurities[RequiredPurity(size)].ValueMod / 100d);
            return total;
        }

        private void RefreshValue()
        {
            double materialValue = RequiredIngotValue(ReadSide(ColumnsTag)) + RequiredIngotValue(ReadSide(RowsTag));
            unitValue = BasePrice + (long)Math.Round(materialValue * 0.5, MidpointRounding.AwayFromZero);
        }

        internal bool CanExpand(bool row)
        {
            if (!CanOpen) return false;
            int nextSize = (row ? Rows : Columns) + 1;
            var ingot = ui.UpgradeSlot.childItem;
            return nextSize <= MaxSide && ingot != null && ingot.unitCount > 0
                && IngotPurityHelper.ingotPurities.TryGetValue(IngotPurityHelper.GetPurity(ingot), out var purity)
                && purity.PurityLevel >= IngotPurityHelper.ingotPurities[RequiredPurity(nextSize)].PurityLevel;
        }

        internal void TryExpand(bool row)
        {
            if (!CanExpand(row)) return;
            var ingot = ui.UpgradeSlot.childItem;
            if (ingot.unitCount == 1) ingot.Destroy();
            else ingot.SetUnitCount(ingot.unitCount - 1);

            int columns = Columns + (row ? 0 : 1);
            int rows = Rows + (row ? 1 : 0);
            ModifyTag(row ? RowsTag : ColumnsTag, tag => tag.Enable().SetInt(row ? rows : columns), skipValidate: true);
            RefreshValue();
            ResizeArea(columns, rows);
        }

        private int ReadSide(string tag) => state.dict.TryGetValue(tag, out var value)
            ? Math.Max(InitialSide, value.GetInt()) : InitialSide;

        private void ResizeArea(int columns, int rows)
        {
            ui.Area.SetShape(columns, rows);
            ui.Refresh();
        }

        private void DisposeArea()
        {
            ui.Close();
            CloseContentWindow(includeChildren: true);
            GeneralHelper.DestroyGameItems(GraphUtils.FindChildrenType<GameItem>(ui.UpgradeWindow));
            ui.UpgradeWindow.Destroy();
            DestroyContentWindow();
            ui = null;
        }

        public override void Destroy()
        {
            if (IsMaterialized) DisposeArea();
            base.Destroy();
        }
    }
}
