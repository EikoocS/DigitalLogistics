using System;
using System.Collections.Generic;
using System.Threading;

namespace DigitalStorage.Storage
{
    public interface StorageProvider : IDisposable
    {
        public string Name { get; }
        public IEnumerable<StoredItem> Items { get; }
        public event Action Changed;
        public abstract IAsyncEnumerable<StoredItem> SearchAsync(StorageQuery query,
            CancellationToken cancellationToken = default);
    }
}
