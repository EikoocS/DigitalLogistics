using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Buses;
using DigitalLogistics.Storage;

namespace DigitalLogistics.Item
{
    internal sealed class FluidStorageBus : Bus
    {
        internal const string ItemId = "fluid_storage_bus";
        private readonly Dictionary<GameItem, FluidProviderRegistry.Link> links = new();
        protected override bool HasLinks => links.Count > 0;
        internal override IEnumerable<object> Configurations => links.Values.Distinct()
            .Select(FluidStorageBusManager.GetConfiguration);
        private FluidStorageBus() : base(ItemId, 120) { }
        internal static GameItem Create() => new FluidStorageBus().Item;
        protected override bool CanTargetExternal(GameItem target) => target is not StorageDisk && FluidEndpoint.IsContainer(target);

        protected override void UpdateLinks(IEnumerable<GameItem> targets)
        {
            var desired = targets.Where(CanTargetExternal).ToHashSet();
            foreach (var item in links.Keys.Except(desired).ToArray()) Release(item);
            foreach (var item in desired)
            {
                if (links.ContainsKey(item)) continue;
                var link = FluidProviderRegistry.Acquire(item);
                if (link == null) continue;
                links.Add(item, link);
                FluidStorageBusManager.Connect(link);
            }
        }

        private void Release(GameItem item)
        {
            FluidStorageBusManager.Disconnect(links[item]);
            FluidProviderRegistry.Release(links[item], item);
            links.Remove(item);
        }
        protected override void ClearLinks()
        {
            foreach (var item in links.Keys.ToArray()) Release(item);
        }
    }
}
