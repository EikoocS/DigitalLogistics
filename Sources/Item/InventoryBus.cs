using DigitalLogistics.Storage.Providers;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Storage;

namespace DigitalLogistics.Item
{
    internal abstract class InventoryBus : Bus
    {
        private readonly Dictionary<GameInventory, SharedProviderRegistry.InventoryLink> inventories = new();
        protected override bool HasLinks => inventories.Count > 0;
        protected IEnumerable<SharedProviderRegistry.InventoryLink> InventoryLinks => inventories.Values;
        protected abstract void OnConnected(SharedProviderRegistry.InventoryLink link);
        protected abstract void OnDisconnected(SharedProviderRegistry.InventoryLink link);

        protected InventoryBus(string id) : base(id) { }

        protected override void UpdateLinks(IEnumerable<GameItem> targets)
        {
            var desired = new Dictionary<GameInventory, GameItem>();
            if (!Disabled && Parent != null && IsInternal)
            {
                var owner = targets.FirstOrDefault();
                if (InventoryStorageProvider.GetPanel(Parent)?.role != "note")
                    desired.Add(Parent, owner is GameCharacterItem ? null : owner);
            }
            else
            {
                foreach (var target in targets)
                    foreach (var inventory in GraphUtils.FindChildrenType<GameInventory>(target.contentWindow))
                        if (InventoryStorageProvider.GetPanel(inventory)?.role != "note")
                            desired[inventory] = target;
            }
            foreach (var inventory in inventories.Keys.ToArray())
            {
                if (desired.ContainsKey(inventory)) continue;
                Release(inventory);
            }
            foreach (var entry in desired)
            {
                if (inventories.ContainsKey(entry.Key)) continue;
                var link = SharedProviderRegistry.AcquireInventory(entry.Key, entry.Value);
                inventories.Add(entry.Key, link);
                OnConnected(link);
            }
        }

        private void Release(GameInventory inventory)
        {
            var link = inventories[inventory];
            OnDisconnected(link);
            SharedProviderRegistry.ReleaseInventory(link);
            inventories.Remove(inventory);
        }

        protected override void ClearLinks()
        {
            foreach (var inventory in inventories.Keys.ToArray()) Release(inventory);
        }
    }
}
