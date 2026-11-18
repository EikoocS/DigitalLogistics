using System;
using DigitalLogistics.Storage.Items;

namespace DigitalLogistics.Storage
{
    public sealed class StorageOutput : IDisposable
    {
        private readonly ItemInstanceLease lease;
        private Action<int> commit;
        public GameItem Item => lease.Item;

        internal StorageOutput(ItemInstanceLease lease, Action<int> commit = null)
        {
            this.lease = lease;
            this.commit = commit;
        }

        public int TryPlace(SlotMarker slot, int amount)
        {
            if (slot == null) return 0;
            amount = Math.Min(amount, Math.Min(slot.numTransfer, Item.MaxNumRemove()));
            if (amount <= 0) return 0;
            if (lease.OwnsInstance) Item.unitCount = amount;
            int moved = slot.TryAcceptOnce(amount);
            if (moved > 0)
            {
                if (slot.targetItem == null) lease.Detach();
                Complete(moved);
            }
            return moved;
        }

        public bool Consume(int amount)
        {
            if (!lease.OwnsInstance && !GameItemTransfer.Remove(Item, amount)) return false;
            Complete(amount);
            return true;
        }

        private void Complete(int moved)
        {
            var callback = commit;
            commit = null;
            callback?.Invoke(moved);
        }

        public void Dispose()
        {
            commit = null;
            lease.Dispose();
        }
    }
}
