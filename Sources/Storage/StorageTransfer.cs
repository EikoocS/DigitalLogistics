using System;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Item;
using DigitalLogistics.Storage.Items;
using DigitalLogistics.Storage.Providers;

namespace DigitalLogistics.Storage
{
    public static class StorageTransfer
    {
        private static StorageOutput Prepare(CachedItem source)
        {
            if (!source.VerifyValidity()) return null;
            if (source is IStoredItem stored) return stored.PrepareOutput();
            var lease = source.AcquireInstance();
            return lease == null ? null : new StorageOutput(lease);
        }

        private static bool CanMove(GameItem item) => item is GameItemElement element && !element.IsDestroyed()
            && !ItemPinHelper.IsPinned(item) && item.unitCount > 0 && (item.parentInventory == null || item.MayRemove())
            && item.MaxNumRemove() > 0 && !ItemMovementProtection.ContainsEnabledDevice(item);

        internal static int TryMoveToInventory(GameItem item, GameInventory target, int amount)
        {
            if (item.parentInventory == target || !CanMove(item) || ItemAccess.IsBlocked(item)
                || ItemAccess.IsBlocked(target.GetParentItem())) return 0;
            amount = Math.Min(amount, item.MaxNumRemove());
            using var output = new StorageOutput(new ItemInstanceLease(item));
            return output.TryPlace(InventoryStorageProvider.FindInputSlot(target, item, amount), amount);
        }

        public static StorageTransferResult TryPlace(CachedItem source, Func<GameItem, SlotMarker> findSlot, int amount)
        {
            using var output = Prepare(source);
            int moved = 0;
            int? placedId = null;
            if (output != null && CanMove(output.Item))
            {
                var slot = findSlot(output.Item);
                if (!ItemPinHelper.IsPinned(slot?.targetItem)) moved = output.TryPlace(slot, amount);
                if (moved > 0) placedId = (slot.targetItem ?? slot.item).GetUniqueID();
            }
            return CreateResult(moved, source, Array.Empty<CachedItem>(), placedId);
        }

        public static StorageTransferResult TryInput(GameItem item, int amount = -1,
            Func<ItemStorageProvider, bool> providerFilter = null) => TryInput(new PhysicalItem(item), amount, providerFilter);

        public static StorageTransferResult TryInput(CachedItem source, int amount = -1,
            Func<ItemStorageProvider, bool> providerFilter = null) => Input(source,
                DigitalLogistics.Manager.ItemProviders.Where(provider => providerFilter == null || providerFilter(provider))
                    .OrderByDescending(provider => provider.Priority).ToArray(), amount, true);

        internal static StorageTransferResult TryInput(CachedItem source, ItemStorageProvider target, int amount = -1) =>
            Input(source, new[] { target }, amount, false);

        private static StorageTransferResult Input(CachedItem source, IEnumerable<ItemStorageProvider> providers,
            int amount, bool applyFilter)
        {
            int remaining;
            using (var lease = source.AcquireInstance())
                remaining = lease == null ? 0 : amount < 0 ? lease.Item.MaxNumRemove()
                    : Math.Min(amount, lease.Item.MaxNumRemove());
            int requested = remaining;
            var items = new Dictionary<int, CachedItem>();
            foreach (var provider in providers)
            {
                if (remaining <= 0) break;
                if (source is IStoredItem stored && stored.Provider == provider) continue;
                while (remaining > 0)
                {
                    using var output = Prepare(source);
                    if (output == null || !CanMove(output.Item)) break;
                    var result = provider.TryInput(output, remaining, applyFilter);
                    if (result.Amount == 0) break;
                    remaining -= result.Amount;
                    foreach (var item in result.Items) items[item.UniqueId] = item;
                }
            }
            return CreateResult(requested - remaining, source, items.Values.ToArray());
        }

        private static StorageTransferResult CreateResult(int amount, CachedItem source,
            IReadOnlyCollection<CachedItem> items, int? placedId = null)
        {
            var remaining = source is IStoredItem stored ? stored.Provider.FindIndexedItem(source.UniqueId)
                : DigitalLogistics.Manager.FindItem(source.UniqueId);
            return new StorageTransferResult(amount, source.UniqueId, remaining, items, placedId);
        }
    }
}
