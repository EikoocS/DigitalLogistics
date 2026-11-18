using System;

namespace DigitalLogistics.Storage.Items
{
    public sealed class ItemInstanceLease : IDisposable
    {
        public GameItem Item { get; }
        internal bool OwnsInstance { get; private set; }

        internal ItemInstanceLease(GameItem item, bool ownsInstance = false)
        {
            Item = item;
            OwnsInstance = ownsInstance;
        }

        internal void Detach() => OwnsInstance = false;

        public void Dispose()
        {
            if (!OwnsInstance) return;
            OwnsInstance = false;
            Item.Destroy();
        }
    }
}
