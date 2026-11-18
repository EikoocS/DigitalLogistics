namespace DigitalLogistics.Storage
{
    internal static class GameItemTransfer
    {
        internal static bool Remove(GameItem item, int count)
        {
            var inventory = item.parentInventory;
            if (count == item.unitCount)
            {
                if (inventory != null && !inventory.Expel(item)) return false;
                item.unitCount = 0;
                var drag = ItemMouseDragHandler.current;
                if (drag == null || !drag.IsDraggingItem || !ReferenceEquals(drag.GetCurrentItem(), item))
                    item.Destroy();
                return true;
            }

            item.unitCount -= count;
            item.Validate();
            item.onChangeAmountItemFunc?.Invoke(item);
            if (inventory != null)
            {
                var slot = inventory.GetIncompleteItemInventorySlot(item);
                item.onSlotChangeAmountItemFunc?.Invoke(item, inventory, slot);
                inventory.onSlotChangeAmountItemFunc?.Invoke(item, inventory, slot);
                OverlayHandler.current.UpdateInventoryStateUI();
            }
            return true;
        }
    }
}
