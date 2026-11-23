using DigitalLogistics.Item;
using UnityEngine;

namespace DigitalLogistics.UI
{
    internal sealed class IndustryAreaUI
    {
        private IndustryDisk disk;
        private readonly MachineryButton rowButton;
        private readonly MachineryButton columnButton;
        private readonly EmptyPixelElement upgradeDock;
        private readonly MachineryStack areaLayout;
        private readonly MachineryPanel areaPanel;

        internal PixelWindow AreaWindow { get; }
        internal PixelWindow UpgradeWindow { get; }
        internal GameGridInventory Area { get; }
        internal GameSlotInventory UpgradeSlot { get; }

        internal IndustryAreaUI(IndustryDisk disk, GameGridInventory area, GameSlotInventory upgradeSlot)
        {
            this.disk = disk;
            Area = area;
            UpgradeSlot = upgradeSlot;
            UpgradeSlot
                .SetBackgroundFadeSprite("Items/items_material", IndustryDisk.IngotId)
                .SetTooltipName(LocHelper.GetLocalizedItem("item_metal_ingot_name"));
            rowButton = CreateAxisButton(row: true);
            columnButton = CreateAxisButton(row: false);
            var upgrade = MachineryStack.Vertical(UpgradeSlot, columnButton, rowButton);
            UpgradeWindow = MachineHelper.CreateMachineryWindow(false, MachineHelper.Panel(null, upgrade))
                .SetTitle(Localize("upgrade_title"));
            UpgradeWindow.closeCallback = () => SetUpgradeVisible(false);
            UpgradeWindow.Validate();
            upgradeDock = new EmptyPixelElement(UpgradeWindow.widthPixels, UpgradeWindow.heightPixels);
            upgradeDock.handler.image.enabled = false;
            upgradeDock.handler.gameObject.SetActive(false);
            UpgradeWindow.handler.SetParent(upgradeDock.handler);
            UpgradeWindow.rectTransform.anchoredPosition = Vector2.zero;

            var upgradeButton = new MachineryButton("+", ToggleUpgrade, 24, 24).SetFillWidth(false)
                .SetDescription(Localize("upgrade"));
            areaLayout = MachineryStack.Horizontal(upgradeButton, Area, upgradeDock)
                .SetAlign(MachineryStack.Align.Start).SetMainAlign(MachineryStack.Align.Start);
            areaPanel = MachineHelper.Panel(null, areaLayout);
            AreaWindow = MachineHelper.CreateMachineryWindow(true, areaPanel);
            AreaWindow.closeCallback = () => SetUpgradeVisible(false);
            AreaWindow.openCallback = Refresh;

            UpgradeSlot.onSlotAddItemEndFunc += OnIngotChanged;
            UpgradeSlot.onSlotRemoveItemEndFunc += OnIngotChanged;
            UpgradeSlot.onSlotChangeAmountItemFunc += OnIngotChanged;
        }

        internal void Bind(IndustryDisk owner) => disk = owner;

        internal void Open()
        {
            if (AreaWindow.IsVisible()) AreaWindow.ToFront();
            else AreaWindow.Show(Input.mousePosition);
        }

        internal void Close()
        {
            SetUpgradeVisible(false);
            AreaWindow.Hide();
        }

        internal void Refresh()
        {
            AreaWindow.SetTitle($"{disk.GetDisplayName(preferCustomName: true)} · {disk.Columns} × {disk.Rows}");
            RefreshUpgrade();
            if (AreaWindow.IsVisible()) AreaWindow.FixBounds();
        }

        private void ToggleUpgrade()
        {
            if (!disk.CanOpen) return;
            RefreshUpgrade();
            SetUpgradeVisible(!upgradeDock.handler.gameObject.activeSelf);
        }

        private void SetUpgradeVisible(bool visible)
        {
            if (upgradeDock.handler.gameObject.activeSelf == visible) return;
            upgradeDock.handler.gameObject.SetActive(visible);
            UpgradeWindow.handler.gameObject.SetActive(visible);
            areaLayout.ResizePixels(0, 0);
            areaPanel.ResizePixels(0, 0);
            AreaWindow.Validate();
            if (AreaWindow.IsVisible()) AreaWindow.FixBounds();
        }

        private MachineryButton CreateAxisButton(bool row) =>
            new MachineryButton(Localize(row ? "add_row" : "add_column"), () => disk.TryExpand(row), 96, 20)
                .SetFillWidth(false);

        private void OnIngotChanged(GameItem _, GameInventory __, SlotMarker ___) => RefreshUpgrade();

        private void RefreshUpgrade()
        {
            RefreshAxis(rowButton, disk.Rows, row: true);
            RefreshAxis(columnButton, disk.Columns, row: false);
        }

        private void RefreshAxis(MachineryButton button, int size, bool row)
        {
            button.SetDisabled(!disk.CanExpand(row));
            if (size >= IndustryDisk.MaxSide)
            {
                button.SetDescription(Localize("max_size"));
                return;
            }
            var purity = IngotPurityHelper.ingotPurities[IndustryDisk.RequiredPurity(size + 1)];
            button.SetDescription(string.Format(Localize("upgrade_requirement"), size, size + 1,
                purity.itemCondition.display));
        }

        private static string Localize(string key) => DigitalLogistics.Localize("ui.industry." + key);
    }
}
