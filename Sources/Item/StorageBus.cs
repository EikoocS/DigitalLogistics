namespace DigitalStorage.Item
{
    internal sealed class StorageBus : InventoryBus
    {
        internal const string ItemId = "storage_bus";
        protected override bool IsStorage => true;

        private StorageBus() : base(ItemId) { }
        internal static GameItem Create() => new StorageBus().Item;
    }
}
