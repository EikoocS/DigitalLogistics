using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Buses;
using DigitalLogistics.Storage;
using DigitalLogistics.Storage.Providers;

namespace DigitalLogistics.Item
{
    internal sealed class FluidIOBus : Bus, ITriggerableBus
    {
        internal const string InputId = "fluid_input_bus";
        internal const string OutputId = "fluid_output_bus";
        internal const string InputOutputId = "fluid_input_output_bus";
        private readonly Dictionary<object, FluidEndpoint> endpoints = new();
        protected override bool HasLinks => endpoints.Count > 0;
        internal override IEnumerable<object> Configurations => FluidIOBusManager.GetConfigurations(Item);
        private FluidIOBus(string id) : base(id) { }
        internal static GameItem Create(string id) => new FluidIOBus(id).Item;
        internal static bool IsIO(GameItem item) => item?.identifier is InputId or OutputId or InputOutputId;
        internal static bool CanInput(GameItem item) => item.identifier != OutputId;
        internal static bool CanOutput(GameItem item) => item.identifier != InputId;
        protected override bool CanTargetExternal(GameItem target) => target is not StorageDisk
            && (FluidEndpoint.IsContainer(target) || base.CanTargetExternal(target));

        protected override void UpdateLinks(IEnumerable<GameItem> targets)
        {
            var desired = new Dictionary<object, GameItem>();
            var targetList = targets.ToArray();
            if (!Disabled && Parent != null && IsInternal && !BusAccess.IsBlocked(Item))
            {
                var owner = targetList.FirstOrDefault();
                if (FluidEndpoint.IsContainer(owner)) desired[owner] = owner;
                else if (InventoryStorageProvider.GetPanel(Parent)?.role != "note")
                    desired[Parent] = owner is GameCharacterItem ? null : owner;
            }
            else
                foreach (var target in targetList.Where(CanTargetExternal))
                    if (FluidEndpoint.IsContainer(target)) desired[target] = target;
                    else
                        foreach (var inventory in GraphUtils.FindChildrenType<GameInventory>(target.contentWindow))
                            if (InventoryStorageProvider.GetPanel(inventory)?.role != "note") desired[inventory] = target;

            foreach (var key in endpoints.Keys.Except(desired.Keys).ToArray()) Release(key);
            foreach (var entry in desired)
            {
                if (endpoints.TryGetValue(entry.Key, out var current))
                {
                    current.Refresh();
                    continue;
                }
                var endpoint = entry.Key is GameInventory inventory
                    ? new FluidEndpoint(entry.Value, inventory) : new FluidEndpoint((GameItem)entry.Key);
                endpoints.Add(entry.Key, endpoint);
                FluidIOBusManager.Acquire(Item, endpoint);
            }
        }

        private void Release(object key)
        {
            FluidIOBusManager.Release(Item, endpoints[key]);
            endpoints[key].Dispose();
            endpoints.Remove(key);
        }
        protected override void ClearLinks()
        {
            foreach (var key in endpoints.Keys.ToArray()) Release(key);
        }
        protected override void OnMoved()
        {
            if (IOBusManager.IsOnMove(Item)) FluidIOBusManager.Schedule(Item);
        }
        public IEnumerable<BusOperation> GetOperations(bool nightly) => FluidIOBusManager.GetOperations(Item, nightly);
    }
}
