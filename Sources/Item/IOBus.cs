using DigitalStorage.Controller;
using DigitalStorage.Storage;
using DigitalStorage.UI;

namespace DigitalStorage.Item
{
    internal static class IOBus
    {
        internal const string InputId = "input_bus";
        internal const string OutputId = "output_bus";
        internal const string InputOutputId = "input_output_bus";

        internal static bool IsIO(GameItem item) => item?.identifier is InputId or OutputId or InputOutputId;
        internal static bool CanInput(GameItem item) => item.identifier != OutputId;
        internal static bool CanOutput(GameItem item) => item.identifier != InputId;
        internal static bool IsBus(GameItem item) => item?.identifier == StorageBus.ItemId || IsIO(item);

        internal static GameItem Create(string id)
        {
            var item = ItemDirectory.CreateEmptyItem(id)
                .SetSpriteAndShapeFromMod(DigitalStorage.Instance.Manifest.ID, id)
                .SetShape(new GridShapeBuilder().SetDataFill(3, 1));
            item.SetContentWindow(BusUI.Create(item));
            item.SetGameItemType("MACHINE");
            item.EnableTag(GeneralHelper.IS_UNLOAD_DISABLED_TAG);
            item.shortDescription = StorageTerminal.Localize($"item.{id}.desc");
            item.flavorText = StorageTerminal.Localize($"item.{id}.flavor");
            item.mayToggleSlotItemFunc = (_, _, _) => true;
            item.canToggleSlotItemFunc = (_, _, _) => true;
            item.onToggleSlotItemFunc += BusController.Toggle;
            item.onSlotAddItemFunc += BusController.OnAdded;
            item.onSlotRemoveItemFunc += BusController.OnRemoved;
            item.onLoaded += BusController.OnLoaded;
            item.mayActivateSlotItemFunc = (_, _, _) => true;
            item.canActivateSlotItemFunc = (self, _, _) => GeneralHelper.IsItemOwned(self)
                && !self.IsTag(StorageBus.DisabledTag);
            item.onActivateSlotItemFunc = (self, _, _) => IOBusManager.Run(self);
            item.EnableTag("CONFIGURE_TAG");
            item.onConfigureSlotItemFunc = (self, _, _) => self.contentWindow.Show();
            BusController.InitAppearance(item);
            return item;
        }
    }
}
