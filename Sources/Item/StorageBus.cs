using DigitalStorage.Controller;
using DigitalStorage.UI;

namespace DigitalStorage.Item
{
    internal static class StorageBus
    {
        internal const string ItemId = "storage_bus";
        internal const string InternalTag = "DIGITAL_STORAGE_BUS_INTERNAL_TAG";
        internal const string DisabledTag = "DIGITAL_STORAGE_BUS_DISABLED_TAG";

        internal static GameItem Create()
        {
            var item = ItemDirectory.CreateEmptyItem(ItemId)
                .SetSpriteAndShapeFromMod(DigitalStorage.Instance.Manifest.ID, ItemId)
                .SetShape(new GridShapeBuilder().SetDataFill(3, 1));
            item.SetContentWindow(BusUI.Create(item));
            item.SetGameItemType("MACHINE");
            item.EnableTag(GeneralHelper.IS_UNLOAD_DISABLED_TAG);
            item.shortDescription = StorageTerminal.Localize("item.storage_bus.desc");
            item.flavorText = StorageTerminal.Localize("item.storage_bus.flavor");
            item.mayToggleSlotItemFunc = (_, _, _) => true;
            item.canToggleSlotItemFunc = (_, _, _) => true;
            item.onToggleSlotItemFunc += BusController.Toggle;
            item.onSlotAddItemFunc += BusController.OnAdded;
            item.onSlotRemoveItemFunc += BusController.OnRemoved;
            item.onLoaded += BusController.OnLoaded;
            BusController.InitAppearance(item);
            return item;
        }
    }
}
