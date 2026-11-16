using System;

namespace DigitalStorage.Storage
{
    public sealed class StorageOutput : IDisposable
    {
        private Action<int> onSucceeded;
        private Action onDisposed;

        public GameItem Item { get; }

        public StorageOutput(GameItem item, Action<int> onSucceeded = null, Action onDisposed = null)
        {
            Item = item;
            this.onSucceeded = onSucceeded;
            this.onDisposed = onDisposed;
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
