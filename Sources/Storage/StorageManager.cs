using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using DigitalLogistics.Collections;
using DigitalLogistics.Filtering;
using DigitalLogistics.Storage.Indexing;
using DigitalLogistics.Storage.Items;

namespace DigitalLogistics.Storage
{
    public class StorageManager
    {
        private readonly HashSet<StorageProvider> providers = new HashSet<StorageProvider>();
        public IEnumerable<StorageProvider> Providers => providers;
        public event Action onChange;
        internal void NotifyChanged() => onChange?.Invoke();

        public void Register(StorageProvider provider)
        {
            if (!providers.Add(provider)) return;
            provider.Changed += NotifyChanged;
            NotifyChanged();
        }

        public void Unregister(StorageProvider provider)
        {
            if (!providers.Remove(provider)) return;
            provider.Changed -= NotifyChanged;
            NotifyChanged();
        }

        public void Clear()
        {
            foreach (var provider in providers) provider.Changed -= NotifyChanged;
            providers.Clear();
            NotifyChanged();
        }

        internal CachedItem FindItem(int uniqueId) => providers.Select(provider => provider.FindIndexedItem(uniqueId))
            .FirstOrDefault(item => item != null);

        internal bool Owns(CachedItem item) => item is IStoredItem stored
            ? providers.Contains(stored.Provider)
            : providers.Any(provider => provider.Contains(item.UniqueId));

        public void Refresh()
        {
            Refresh((_) => true);
        }

        internal IEnumerable<CachedItem> SearchForOutput(StoredItemExpression expression,
            StorageProvider excluded)
        {
            foreach (var provider in providers.OrderByDescending(provider => provider.Priority).ToArray())
            {
                if (provider == excluded) continue;
                foreach (var item in provider.Snapshot())
                    if (provider.Filter.Evaluate(item) && expression.Evaluate(item)) yield return item;
            }
        }

        public void Refresh(Func<StorageProvider, bool> providerFilter)
        {
            foreach (var provider in providers) 
            { 
                if (providerFilter(provider)) provider.Refresh();
            }
        }

        public async IAsyncEnumerable<CachedItem> SearchAsync(
            StoredItemExpression expression,
            StorageSort.SortBy sortBy = StorageSort.SortBy.IDENTIFIER,
            bool reverse = false,
            Func<StorageProvider, bool> providerFilter = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default,
            bool prioritizeOutput = false)
        {
            var providersCopy = new List<StorageProvider>(providers);
            var itemComparer = sortBy.Comparer();
            var comparer = ComparerHelper.ComparedBy((IAsyncEnumerator<CachedItem> enumerator) => enumerator.Current, itemComparer);
            var priorities = new Dictionary<IAsyncEnumerator<CachedItem>, StorageProvider>();
            if (prioritizeOutput)
                comparer = Comparer<IAsyncEnumerator<CachedItem>>.Create((a, b) =>
                    reverse ? priorities[b].Priority.CompareTo(priorities[a].Priority)
                        : priorities[a].Priority.CompareTo(priorities[b].Priority)).LinkComparer(comparer);
            var heads = new OrderList<IAsyncEnumerator<CachedItem>>(comparer);
            IAsyncEnumerator<CachedItem> pendingEnumerator = null;

            try
            {
                foreach (var provider in providersCopy)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (providerFilter != null && !providerFilter(provider)) continue;

                    pendingEnumerator = provider.SearchAsync(expression, sortBy, reverse, cancellationToken)
                        .GetAsyncEnumerator();
                    priorities[pendingEnumerator] = provider;
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
