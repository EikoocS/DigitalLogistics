namespace DigitalStorage.Item
{
    internal static class StorageTerminal
    {
        internal const string ItemId = "storage_terminal";

        internal static string Localize(string key) =>
            ModHelper.GetLocalized(DigitalStorage.Instance.Manifest.ID, key);

        internal static GameItem Create()
        {
            var item = ItemDirectory.CreateEmptyItem(ItemId)
                .SetSpriteAndShapeFromMod(DigitalStorage.Instance.Manifest.ID, ItemId)
                .SetShape(new GridShapeBuilder().SetDataFill(3, 3))
                .SetContentWindow(StorageTerminalUI.Create());
            item.name = Localize("item.storage_terminal.name");
            item.shortDescription = Localize("item.storage_terminal.desc");
            item.flavorText = Localize("item.storage_terminal.flavor");
            item.SetGameItemType("MACHINE");
            return item;
        }
    }
}
