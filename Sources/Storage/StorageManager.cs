using DigitalStorage.Filtering;
using DigitalStorage.Collections;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DigitalStorage.Storage
{
    public class StorageManager
    {
        private readonly HashSet<StorageProvider> providers = new HashSet<StorageProvider>();
        public IEnumerable<StorageProvider> Providers => providers;
        public void registerStorageProvider(StorageProvider provider)
        {
            providers.Add(provider);
        }

        public void unregisterStorageProvider(StorageProvider provider)
        {
            providers.Remove(provider);
            provider.Dispose();
        }

        public void Refresh()
        {
            Refresh((_) => true);
        }

        public StorageInputResult TryInput(GameItem item, int amount = -1)
        {
            int remaining = amount < 0 ? item.unitCount : Math.Min(amount, item.unitCount);
            int requested = remaining;
            var items = new Dictionary<int, StoredItem>();
            foreach (var provider in new List<StorageProvider>(providers))
            {
                if (remaining <= 0) break;
                var result = provider.TryInput(item, remaining);
                remaining -= result.Amount;
                foreach (var stored in result.Items) items[stored.UniqueId] = stored;
            }
            return new StorageInputResult(requested - remaining, new List<StoredItem>(items.Values));
        }

        public void Refresh(Func<StorageProvider, bool> providerFilter)
        {
            foreach (var provider in providers) 
            { 
                if (providerFilter(provider)) provider.Refresh();
            }
        }

        public async IAsyncEnumerable<StoredItem> SearchAsync(
            StoredItemExpression expression,
            StorageSort.SortBy sortBy = StorageSort.SortBy.IDENTIFIER,
            bool reverse = false,
            Func<StorageProvider, bool> providerFilter = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var providersCopy = new List<StorageProvider>(providers);
            var itemComparer = ComparerHelper.ComparedBy(sortBy.Getter());
            var comparer = ComparerHelper.ComparedBy((IAsyncEnumerator<StoredItem> enumerator) => enumerator.Current, itemComparer);
            var heads = new OrderList<IAsyncEnumerator<StoredItem>>(comparer);
            IAsyncEnumerator<StoredItem> pendingEnumerator = null;

            try
            {
                foreach (var provider in providersCopy)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (providerFilter != null && !providerFilter(provider)) continue;

                    if (!provider.PrepareSearch(expression)) continue;

                    pendingEnumerator = provider.SearchAsync(expression, sortBy, reverse, cancellationToken)
                        .GetAsyncEnumerator();
                    if (await pendingEnumerator.MoveNextAsync())
                        heads.Add(pendingEnumerator);
                    else
                        await pendingEnumerator.DisposeAsync();
                    pendingEnumerator = null;
                }

                while (heads.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    pendingEnumerator = heads.Pop(bottom: !reverse);
                    yield return pendingEnumerator.Current;

                    if (await pendingEnumerator.MoveNextAsync())
                        heads.Add(pendingEnumerator);
                    else
                        await pendingEnumerator.DisposeAsync();
                    pendingEnumerator = null;
                }

            }
            finally
            {
                if (pendingEnumerator != null)
                    await pendingEnumerator.DisposeAsync();
                foreach (var enumerator in heads)
                    await enumerator.DisposeAsync();
            }
        }
    }
}
