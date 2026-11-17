using System.Collections.Generic;
using System.Linq;
using DigitalStorage.Storage;

namespace DigitalStorage.Item
{
    internal abstract class InventoryBus : Bus
    {
        private readonly HashSet<GameInventory> inventories = new();
        protected abstract bool IsStorage { get; }
        protected override bool HasLinks => inventories.Count > 0;
        internal override IEnumerable<object> Configurations => IsStorage
            ? inventories.Select(BusManager.GetConfiguration).Cast<object>()
            : IOBusManager.GetConfigurations(Item).Cast<object>();

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
            foreach (var inventory in inventories.ToArray())
            {
                if (desired.ContainsKey(inventory)) continue;
                Release(inventory);
            }
            foreach (var entry in desired)
            {
                if (inventories.Contains(entry.Key)) continue;
                var link = BusManager.Acquire(entry.Key, entry.Value, IsStorage);
                if (!IsStorage) IOBusManager.Acquire(Item, link);
                inventories.Add(entry.Key);
            }
        }

        private void Release(GameInventory inventory)
        {
            if (!IsStorage) IOBusManager.Release(Item, inventory);
            BusManager.Release(inventory, IsStorage);
            inventories.Remove(inventory);
        }

        protected override void ClearLinks()
        {
            foreach (var inventory in inventories.ToArray()) Release(inventory);
        }
    }
}
