using System;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Item;

namespace DigitalLogistics.Storage
{
    internal sealed class FluidEndpoint : IDisposable
    {
        private readonly GameItem owner;
        private readonly GameItem container;
        private readonly GameInventory inventory;
        private readonly Dictionary<GameItem, FluidProviderRegistry.Link> containers = new();
        internal string Key { get; }
        internal string Name { get; }
        internal bool Available => !ItemAccess.IsPinnedOrBlocked(owner);
        internal IEnumerable<FluidStorageProvider> Providers => containers.Values.Select(link => link.Provider);

        internal static bool IsContainer(GameItem item) => item is GameItemElement element
            && !element.IsDestroyed() && item.unitCount > 0 && item.IsTag(WaterHelper.LIQUID_CONTAINER_TAG);

        internal FluidEndpoint(GameItem container) : this(container, null)
        {
        }

        internal FluidEndpoint(GameItem owner, GameInventory inventory)
        {
            this.owner = owner;
            this.inventory = inventory;
            container = inventory == null ? owner : null;
            Key = inventory == null ? "fluid:" + owner.GetUniqueID() : SharedProviderRegistry.GetKey(inventory, owner);
            Name = inventory == null ? owner.GetDisplayName(preferCustomName: true)
                : SharedProviderRegistry.GetName(inventory, owner);
        }

        internal void Refresh()
        {
            var desired = (Available
                ? inventory == null ? new[] { container } : inventory.childItems.ToArray()
                : Array.Empty<GameItem>()).Where(item => IsContainer(item) && !ItemAccess.IsBlocked(item)).ToHashSet();
            foreach (var item in containers.Keys.Except(desired).ToArray()) Release(item);
            foreach (var item in desired)
            {
                if (containers.ContainsKey(item)) continue;
                if (FluidProviderRegistry.Acquire(item) is { } link)
                    containers.Add(item, link);
            }
        }

        private void Release(GameItem item)
        {
            FluidProviderRegistry.Release(containers[item], item);
            containers.Remove(item);
        }

        public void Dispose()
        {
            foreach (var item in containers.Keys.ToArray()) Release(item);
        }
    }
}
