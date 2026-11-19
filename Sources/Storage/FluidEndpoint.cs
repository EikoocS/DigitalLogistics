using System;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Buses;

namespace DigitalLogistics.Storage
{
    internal sealed class FluidEndpoint : IDisposable
    {
        private readonly GameItem owner;
        private readonly GameItem container;
        private readonly GameInventory inventory;
        private readonly Dictionary<GameItem, FluidProviderRegistry.Link> containers = new();
        private readonly Action<bool> liquidChanged;
        internal string Key { get; }
        internal string Name { get; }
        internal bool Available => !BusAccess.IsPinnedOrBlocked(owner);
        internal IEnumerable<FluidStorageProvider> Providers => containers.Values.Select(link => link.Provider);
        internal event Action Added;

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
            liquidChanged = increased => { if (increased && Available) Added?.Invoke(); };
            if (inventory != null)
            {
                inventory.onSlotAddItemEndFunc += OnAdded;
                inventory.onSlotRemoveItemEndFunc += OnRemoved;
                inventory.onSlotChangeAmountItemFunc += OnRemoved;
            }
            Refresh();
        }

        internal void Refresh()
        {
            var desired = (Available
                ? inventory == null ? new[] { container } : inventory.childItems.ToArray()
                : Array.Empty<GameItem>()).Where(item => IsContainer(item) && !BusAccess.IsBlocked(item)).ToHashSet();
            foreach (var item in containers.Keys.Except(desired).ToArray()) Release(item);
            foreach (var item in desired)
            {
                if (containers.ContainsKey(item)) continue;
                var link = FluidProviderRegistry.Acquire(item);
                containers.Add(item, link);
                LiquidChangeEvents.Subscribe(item, liquidChanged);
            }
        }

        private void OnAdded(GameItem item, GameInventory _, SlotMarker __)
        {
            Refresh();
            if (IsContainer(item) && !ItemPinHelper.IsPinned(item)) Added?.Invoke();
        }
        private void OnRemoved(GameItem _, GameInventory __, SlotMarker ___) => Refresh();

        private void Release(GameItem item)
        {
            LiquidChangeEvents.Unsubscribe(item, liquidChanged);
            FluidProviderRegistry.Release(containers[item], item);
            containers.Remove(item);
        }

        public void Dispose()
        {
            if (inventory != null)
            {
                inventory.onSlotAddItemEndFunc -= OnAdded;
                inventory.onSlotRemoveItemEndFunc -= OnRemoved;
                inventory.onSlotChangeAmountItemFunc -= OnRemoved;
            }
            foreach (var item in containers.Keys.ToArray()) Release(item);
        }
    }
}
