using System;

namespace DigitalLogistics.Storage
{
    public sealed class StorageOutput : IDisposable
    {
        private Action<int> onSucceeded;
        private Action onDisposed;
        private readonly bool wholePlacement;

        public GameItem Item { get; }

        public StorageOutput(GameItem item, Action<int> onSucceeded = null, Action onDisposed = null)
            : this(item, onSucceeded, onDisposed, false) { }

        internal StorageOutput(GameItem item, Action<int> onSucceeded, Action onDisposed, bool wholePlacement)
        {
            Item = item;
            this.onSucceeded = onSucceeded;
            this.onDisposed = onDisposed;
            this.wholePlacement = wholePlacement;
        }

        /// <summary>Places up to the requested amount; report the return value through Complete.</summary>
        public int TryPlace(SlotMarker slot, int amount)
        {
            if (slot == null) return 0;
            if (!wholePlacement) return slot.TryAcceptOnce(amount);

            amount = Math.Min(amount, Math.Min(slot.numTransfer, Item.unitCount));
            if (amount <= 0) return 0;
            Item.unitCount = amount;
            return slot.TryAcceptOnce(amount);
        }

        /// <summary>Confirms the actual transferred count once; zero leaves the source unchanged.</summary>
        public void Complete(int moved)
        {
            var callback = onSucceeded;
            onSucceeded = null;
            if (moved > 0) callback?.Invoke(moved);
        }

        public void Dispose()
        {
            onSucceeded = null;
            var callback = onDisposed;
            onDisposed = null;
            callback?.Invoke();
        }
    }
}
