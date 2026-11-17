using System.Collections.Generic;
using DigitalLogistics.Storage;

namespace DigitalLogistics.Item
{
    internal sealed class IOBus : InventoryBus, ITriggerableBus
    {
        internal const string InputId = "input_bus";
        internal const string OutputId = "output_bus";
        internal const string InputOutputId = "input_output_bus";
        protected override bool IsStorage => false;

        private IOBus(string id) : base(id) { }
        internal static GameItem Create(string id) => new IOBus(id).Item;
        internal static bool IsIO(GameItem item) => item?.identifier is InputId or OutputId or InputOutputId;
        internal static bool CanInput(GameItem item) => item.identifier != OutputId;
        internal static bool CanOutput(GameItem item) => item.identifier != InputId;

        protected override void OnMoved()
        {
            if (IOBusManager.IsOnMove(Item)) IOBusManager.Schedule(Item);
        }

        public IEnumerable<BusOperation> GetOperations(bool nightly) => IOBusManager.GetOperations(Item, nightly);
    }
}
