using System.Collections.Generic;
using System.Linq;
using DigitalStorage.Storage;

namespace DigitalStorage.Item
{
    internal sealed class TriggerBus : Bus, ITriggerableBus
    {
        internal const string ItemId = "trigger_bus";
        protected override bool HasLinks => TriggerBusManager.GetConfigurations(Item).Any();
        internal override IEnumerable<object> Configurations => TriggerBusManager.GetConfigurations(Item);

        private TriggerBus() : base(ItemId) { }
        internal static GameItem Create() => new TriggerBus().Item;
        protected override void UpdateLinks(IEnumerable<GameItem> targets) => TriggerBusManager.Connect(Item, targets);
        protected override void ClearLinks() => TriggerBusManager.Disconnect(Item);
        public IEnumerable<BusOperation> GetOperations(bool nightly) =>
            TriggerBusManager.GetOperations(Item, nightly);
    }
}
