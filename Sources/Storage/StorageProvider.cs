using System;
using DigitalLogistics.Filtering;

namespace DigitalLogistics.Storage
{
    public abstract class StorageProvider : IDisposable
    {
        private readonly Func<bool> canAccess;
        public int Priority { get; set; }
        public string Name { get; set; } = "Unnamed";
        public CachedItemExpression Filter { get; set; } = CachedItemExpression.Constant(true);
        internal bool Available => !Disposed && (canAccess?.Invoke() ?? true);
        public bool Disposed { get; private set; }
        public event Action Changed;

        protected StorageProvider(Func<bool> canAccess = null) => this.canAccess = canAccess;
        protected void NotifyChanged() => Changed?.Invoke();

        public void Dispose()
        {
            Disposed = true;
            DisposeCore();
        }

        protected abstract void DisposeCore();
    }
}
