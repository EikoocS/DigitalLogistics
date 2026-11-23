using DigitalLogistics.UI;

namespace DigitalLogistics.Item
{
    internal static class IndustryTerminal
    {
        internal const string ItemId = "industry_terminal";

        internal static GameItem Create()
        {
            var item = ItemDirectory.CreateEmptyItem(ItemId)
                .SetSpriteAndShapeFromMod(DigitalLogistics.Instance.Manifest.ID, ItemId)
                .SetShape(new GridShapeBuilder().SetDataFill(3, 3));
            item.name = DigitalLogistics.Localize("item.industry_terminal.name");
            item.shortDescription = DigitalLogistics.Localize("item.industry_terminal.desc");
            item.flavorText = DigitalLogistics.Localize("item.industry_terminal.flavor");
            item.SetGameItemType("MACHINE");
            item.SetContentWindow(IndustryTerminalUI.Create());
            return item;
        }
    }
}
