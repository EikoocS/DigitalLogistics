using DigitalLogistics.Buses;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Storage;

namespace DigitalLogistics.Item
{
    internal sealed class StorageBus : InventoryBus
    {
        internal const string ItemId = "storage_bus";
        internal override IEnumerable<object> Configurations =>
            InventoryLinks.Select(link => StorageBusManager.GetConfiguration(link.Inventory));
        protected override void OnConnected(SharedProviderRegistry.InventoryLink link) => StorageBusManager.Connect(link);
        protected override void OnDisconnected(SharedProviderRegistry.InventoryLink link) => StorageBusManager.Disconnect(link);

        private StorageBus() : base(ItemId) { }
        internal static GameItem Create() => new StorageBus().Item;

        protected override bool CanTargetExternal(GameItem target) =>
            target is not StorageDisk && base.CanTargetExternal(target);
    }
}
