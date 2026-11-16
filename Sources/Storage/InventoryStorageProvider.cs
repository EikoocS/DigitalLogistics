namespace DigitalStorage.Storage
{
    // StorageProvider bound to GameInventory
    public class InventoryStorageProvider : StorageProvider
    {
        private readonly GameInventory inventory;
        public bool Disposed { get; private set; } = false;

        internal static MachineryPanel GetPanel(GameInventory inventory) =>
            (inventory as PixelElement)?.handler?.FindParentElement<MachineryPanel>();


        public InventoryStorageProvider(GameInventory inventory, string name = "Unnamed")
        {
            this.inventory = inventory;
            this.Name = name;
            
            this.inventory.onSlotAddItemEndFunc += onSlotAddItemEndFunc;
            this.inventory.onSlotRemoveItemEndFunc += onSlotRemoveItemEndFunc;
            this.inventory.onToggleSlotItemFunc += OnItemChanged;
            this.inventory.onSlotChangeAmountItemFunc += OnItemChanged;

            Refresh();
        }

        public override void Dispose()
        {
            Disposed = true;
            inventory.onSlotAddItemEndFunc -= onSlotAddItemEndFunc;
            inventory.onSlotRemoveItemEndFunc -= onSlotRemoveItemEndFunc;
            inventory.onToggleSlotItemFunc -= OnItemChanged;
            inventory.onSlotChangeAmountItemFunc -= OnItemChanged;
        }

        private void  onSlotAddItemEndFunc(GameItem item, GameInventory inventory, SlotMarker slot)
        {
            AddItem(item);
            NotifyChanged();
        }

        private void onSlotRemoveItemEndFunc(GameItem item, GameInventory inventory, SlotMarker slot)
        {
            if (storageIndex.Contains(item.uniqueId))
            {
                storageIndex.Remove(item.uniqueId);
                NotifyChanged();
            }
        }

        private void AddItem(GameItem item)
        {
            if (!storageIndex.Contains(item.uniqueId))
            {
                storageIndex.Add(new PhysicalStoredItem(item, this));
            }
        }

        private void OnItemChanged(GameItem item, GameInventory inventory, SlotMarker slot)
        {
            storageIndex.Remove(item.uniqueId);
            AddItem(item);
            NotifyChanged();
        }

        public override bool Contains(int uniqueId)
        {
            foreach (var item in inventory.childItems)
            {
                if (item.uniqueId == uniqueId) return true;
            }
            return false;
        }

        protected override int InputCore(GameItem item, int amount)
        {
            if (item.parentInventory == inventory) return 0;

            int remaining = amount;
            while (remaining > 0 && !Disposed)
            {
                var slot = FindInputSlot(item, remaining);
                int moved = slot?.TryAcceptOnce(remaining) ?? 0;
                if (moved == 0) break;
                remaining -= moved;
            }
            return amount - remaining;
        }

        private SlotMarker FindInputSlot(GameItem item, int amount)
        {
            if (!inventory.MayHaveValidInventorySlot(item)) return null;

            foreach (var target in inventory.childItems)
            {
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
                            if (slot.numTransfer > 0) return slot;
                        }
            return null;
        }

        public override void Refresh()
        {
            storageIndex.Clear();
            foreach (var item in inventory.childItems)
            {
                storageIndex.Add(new PhysicalStoredItem(item, this));
            }
            NotifyChanged();
        }
    }
}
