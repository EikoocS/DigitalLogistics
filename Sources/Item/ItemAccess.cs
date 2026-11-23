namespace DigitalLogistics.Item
{
    internal static class ItemAccess
    {
        internal static bool IsPinnedOrBlocked(GameItem item) => ItemPinHelper.IsPinned(item) || IsBlocked(item);

        internal static bool IsBlocked(GameItem item)
        {
            for (var current = item; current != null; current = current.parentInventory?.GetParentItem())
                if (!GeneralHelper.IsItemOwned(current) || GeneralHelper.IsItemLocked(current)
                    || current.contentWindow?.isLocked == true)
                    return true;
            return false;
        }
    }
}
