using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System;
using System.Threading;

namespace DigitalStorage.Storage
{
    public class StorageManager
    {
        private readonly HashSet<StorageProvider> providers = new HashSet<StorageProvider>();
        public IReadOnlyCollection<StorageProvider> Providers => providers;
        public event Action Changed;

        private void OnChanged() => Changed?.Invoke();

        public void registerStorageProvider(StorageProvider provider)
        {
            if (!providers.Add(provider)) return;
            provider.Changed += OnChanged;
            OnChanged();
        }

        public void unregisterStorageProvider(StorageProvider provider)
        {
            providers.Remove(provider);
            provider.Changed -= OnChanged;
            provider.Dispose();
            OnChanged();
        }

        internal StoredItem FindItem(string displayName, ISet<GameItem> excludedItems)
        {
            foreach (var provider in providers)
                foreach (var item in provider.Items)
                    if (item.IsAvailable && item.DisplayName == displayName
                        && !excludedItems.Contains(item.Item)
                        && item.Item.MayRemove() && item.Item.MaxNumRemove() > 0)
                        return item;
            return null;
        }

        public async IAsyncEnumerable<StoredItem> SearchAsync(StorageQuery query,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var enumerators = new List<IAsyncEnumerator<StoredItem>>();

            var itemComparer = query.GetComparer();
            var comparer = Comparer<IAsyncEnumerator<StoredItem>>.Create((a, b) => itemComparer.Compare(a.Current, b.Current));

            var heads = new StorageIndex<IAsyncEnumerator<StoredItem>>(comparer);

            try
            {
                foreach (var provider in new List<StorageProvider>(providers))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var enumerator = provider.SearchAsync(query, cancellationToken).GetAsyncEnumerator();
                    enumerators.Add(enumerator);

                    if (await enumerator.MoveNextAsync())
                    {
                        heads.Add(enumerator);
                    }
                }

                while (heads.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int index = query.descending ? heads.Count - 1 : 0;
                    var enumerator = heads[index];
                    heads.Remove(enumerator);

                    yield return enumerator.Current;

                    if (await enumerator.MoveNextAsync())
                    {
                        heads.Add(enumerator);
                    }
                }
            }
            finally
            {
                foreach (var enumerator in enumerators)
                {
                    await enumerator.DisposeAsync();
                }
            }
        }
    }
}
