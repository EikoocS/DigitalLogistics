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
            item.SetContentWindow(StorageBusUI.Create(item));
            item.SetGameItemType("MACHINE");
            item.shortDescription = StorageTerminal.Localize("item.storage_bus.desc");
            item.flavorText = StorageTerminal.Localize("item.storage_bus.flavor");
            item.mayToggleSlotItemFunc = (_, _, _) => true;
            item.canToggleSlotItemFunc = (_, _, _) => true;
            item.onToggleSlotItemFunc += StorageBusController.Toggle;
            item.onSlotAddItemFunc += StorageBusController.OnAdded;
            item.onSlotRemoveItemFunc += StorageBusController.OnRemoved;
            item.onLoaded += StorageBusController.OnLoaded;
            UpdateName(item);
            return item;
        }

        internal static void UpdateName(GameItem item)
        {
            item.name = StorageTerminal.Localize(item.IsTag(DisabledTag)
                ? "item.storage_bus.disabled.name"
                : item.IsTag(InternalTag) ? "item.storage_bus.internal.name" : "item.storage_bus.external.name");
            item.contentWindow.SetTitle(item.GetDisplayName(preferCustomName: true));
        }
    }
}
