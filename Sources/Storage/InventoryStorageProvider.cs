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
