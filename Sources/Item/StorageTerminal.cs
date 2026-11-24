using DigitalLogistics.UI;

namespace DigitalLogistics.Item
{
    internal static class StorageTerminal
    {
        internal const string ItemId = "storage_terminal";

        internal static GameItem Create()
        {
            var item = ItemDirectory.CreateEmptyItem(ItemId)
                .SetSpriteAndShapeFromMod(DigitalLogistics.Instance.Manifest.ID, ItemId)
                .SetShape(new GridShapeBuilder().SetDataFill(3, 3))
                .SetValue(400);
            item.SetContentWindow(StorageTerminalUI.Create(item));
            item.name = DigitalLogistics.Localize("item.storage_terminal.name");
            item.shortDescription = DigitalLogistics.Localize("item.storage_terminal.desc");
            item.flavorText = DigitalLogistics.Localize("item.storage_terminal.flavor");
            item.SetGameItemType("MACHINE");
            return item;
        }
    }
}
