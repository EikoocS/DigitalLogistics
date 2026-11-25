using DigitalLogistics.Buses;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Storage;

namespace DigitalLogistics.Item
{
    internal sealed class IOBus : InventoryBus, ITransferBus
    {
        internal const string InputId = "input_bus";
        internal const string OutputId = "output_bus";
        internal const string InputOutputId = "input_output_bus";
        internal override IEnumerable<object> Configurations => IOBusManager.GetConfigurations(Item);
        protected override void OnConnected(SharedProviderRegistry.InventoryLink link) => IOBusManager.Acquire(Item, link);
        protected override void OnDisconnected(SharedProviderRegistry.InventoryLink link) => IOBusManager.Release(Item, link.Inventory);

        private IOBus(string id) : base(id, 200) { }
        internal static GameItem Create(string id) => new IOBus(id).Item;
        internal static bool CanInput(GameItem item) => item.identifier != OutputId;
        internal static bool CanOutput(GameItem item) => item.identifier != InputId;

        public IEnumerable<LogisticsScheduler.Binding> Transfers =>
            IOBusManager.GetConfigurations(Item).SelectMany(connection => connection.Polling);
    }
}
