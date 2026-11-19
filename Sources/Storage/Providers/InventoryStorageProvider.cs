using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Storage;
using DigitalLogistics.Storage.Items;
using DigitalLogistics.Buses;

namespace DigitalLogistics.Storage.Providers
{
    public sealed class InventoryStorageProvider : ItemStorageProvider
    {
        private readonly GameInventory inventory;
        private Dictionary<int, CachedItem> inputItems;

        internal static MachineryPanel GetPanel(GameInventory inventory) =>
            (inventory as PixelElement)?.handler?.FindParentElement<MachineryPanel>();

        public InventoryStorageProvider(GameInventory inventory, string name = "Unnamed")
            : base(() => !BusAccess.IsBlocked(inventory.GetParentItem()))
        {
            this.inventory = inventory;
            Name = name;
            inventory.onSlotAddItemEndFunc += OnItemChanged;
            inventory.onSlotRemoveItemEndFunc += OnItemRemoved;
            inventory.onToggleSlotItemFunc += OnItemChanged;
            inventory.onSlotChangeAmountItemFunc += OnItemChanged;
            Refresh();
        }

        protected override void DisposeItems()
        {
            inventory.onSlotAddItemEndFunc -= OnItemChanged;
            inventory.onSlotRemoveItemEndFunc -= OnItemRemoved;
            inventory.onToggleSlotItemFunc -= OnItemChanged;
            inventory.onSlotChangeAmountItemFunc -= OnItemChanged;
        }

        private void OnItemRemoved(GameItem item, GameInventory inventory, SlotMarker slot)
        {
            Remove(item.uniqueId);
            inputItems?.Remove(item.uniqueId);
        }

        private void OnItemChanged(GameItem item, GameInventory inventory, SlotMarker slot)
        {
            if (!Available) return;
            if (item.parentInventory != this.inventory || item.unitCount <= 0 || ItemPinHelper.IsPinned(item))
            {
                OnItemRemoved(item, inventory, slot);
                return;
            }
            var stored = new StoredPhysicalItem(item, this);
            Upsert(stored);
            if (inputItems != null) inputItems[item.uniqueId] = stored;
        }

        public override bool Contains(int uniqueId) => Available
            && inventory.childItems.Any(item => item.uniqueId == uniqueId && !ItemPinHelper.IsPinned(item));

        internal void OnPinChanged(GameItem item) => OnItemChanged(item, inventory, null);

        protected override StorageInputResult InputCore(StorageOutput output, int amount)
        {
            if (output.Item.parentInventory == inventory) return StorageInputResult.Empty;
            inputItems = new Dictionary<int, CachedItem>();
            try
            {
                int moved = output.TryPlace(FindInputSlot(output.Item, amount), amount);
                return new StorageInputResult(moved, inputItems.Values.ToArray());
            }
            finally { inputItems = null; }
        }

        private SlotMarker FindInputSlot(GameItem item, int amount)
        {
            if (!inventory.childItems.Any(ItemPinHelper.IsPinned))
                MachineFeedDispenser.FlushPendingAutoBlend(inventory);
            if (!inventory.MayHaveValidInventorySlot(item)) return null;

            foreach (var target in inventory.childItems)
            {
                if (ItemPinHelper.IsPinned(target)) continue;
                var slot = target.GetStackSlot(item);
                if (slot?.numTransfer > 0) return slot;
            }

            var (width, height) = inventory switch
            {
                GameGridInventory grid => (grid.inventoryShape.width, grid.inventoryShape.height),
                GameGridScrollableInventory grid => (grid.width, grid.height),
                _ => (1, 1)
            };
            var shape = new GridShapeBuilder(item.modifiedShape);
            for (int flip = 0; flip < 2; flip++)
                for (int rotation = 0; rotation < 4; rotation++)
                    for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++)
                        {
                            shape.SetTransform(x, y, flip != 0, 4 - rotation);
                            var slot = inventory.TryInventorySlot(item, amount, shape);
                            if (slot.numTransfer > 0 && !ItemPinHelper.IsPinned(slot.targetItem)) return slot;
                        }
            return null;
        }

        protected override IEnumerable<CachedItem> ReadItems()
        {
            if (!Available) yield break;
            foreach (var item in inventory.childItems.ToArray())
            {
                if (!Available) yield break;
                if (item.parentInventory == inventory && item.unitCount > 0 && !ItemPinHelper.IsPinned(item))
                    yield return new StoredPhysicalItem(item, this);
            }
        }
    }
}
