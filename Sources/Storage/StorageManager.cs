using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using DigitalLogistics.Collections;
using DigitalLogistics.Filtering;
using DigitalLogistics.Storage.Indexing;
using DigitalLogistics.Storage.Items;
using DigitalLogistics.Tracing;

namespace DigitalLogistics.Storage
{
    public class StorageManager
    {
        private readonly HashSet<StorageProvider> providers = new HashSet<StorageProvider>();
        public IEnumerable<StorageProvider> Providers => providers;
        public IEnumerable<ItemStorageProvider> ItemProviders => providers.OfType<ItemStorageProvider>();
        public IEnumerable<FluidStorageProvider> FluidProviders => providers.OfType<FluidStorageProvider>();
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

        internal CachedItem FindItem(int uniqueId) => ItemProviders.Select(provider => provider.FindIndexedItem(uniqueId))
            .FirstOrDefault(item => item != null);

        internal bool Owns(CachedItem item) => item is IStoredItem stored
            ? providers.Contains(stored.Provider)
            : ItemProviders.Any(provider => provider.Contains(item.UniqueId));

        public void Refresh()
        {
            Refresh((_) => true);
        }

        internal IEnumerable<CachedItem> SearchForOutput(CachedItemExpression expression,
            ItemStorageProvider excluded)
        {
            foreach (var provider in ItemProviders.OrderByDescending(provider => provider.Priority).ToArray())
            {
                if (provider == excluded) continue;
                foreach (var item in provider.Snapshot())
                    if (provider.Filter.Evaluate(item) && expression.Evaluate(item)) yield return item;
            }
        }

        public void Refresh(Func<StorageProvider, bool> providerFilter)
        {
            foreach (var provider in ItemProviders) 
            { 
                if (providerFilter(provider)) provider.Refresh();
            }
        }

        public IAsyncEnumerable<CachedItem> SearchAsync(
            CachedItemExpression expression,
            StorageSort.SortBy sortBy = StorageSort.SortBy.IDENTIFIER,
            bool reverse = false,
            Func<StorageProvider, bool> providerFilter = null,
            CancellationToken cancellationToken = default,
            bool prioritizeOutput = false) =>
            SearchAsync(null, expression, sortBy, reverse, providerFilter, cancellationToken, prioritizeOutput);

        internal async IAsyncEnumerable<CachedItem> SearchAsync(
            SearchMeasurement trace,
            CachedItemExpression expression,
            StorageSort.SortBy sortBy = StorageSort.SortBy.IDENTIFIER,
            bool reverse = false,
            Func<StorageProvider, bool> providerFilter = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default,
            bool prioritizeOutput = false)
        {
            var timer = new TraceTimer(trace != null);
            var providersCopy = new List<ItemStorageProvider>(ItemProviders);
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

                    pendingEnumerator = provider.SearchAsync(trace, expression, sortBy, reverse, cancellationToken)
                        .GetAsyncEnumerator();
                    priorities[pendingEnumerator] = provider;
                    timer.Pause();
                    bool found = await pendingEnumerator.MoveNextAsync();
                    timer.Resume();
                    if (found)
                        heads.Add(pendingEnumerator);
                    else
                        await pendingEnumerator.DisposeAsync();
                    pendingEnumerator = null;
                }

                while (heads.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    pendingEnumerator = heads.Pop(bottom: !reverse);
                    timer.Pause();
                    yield return pendingEnumerator.Current;

                    bool found = await pendingEnumerator.MoveNextAsync();
                    timer.Resume();
                    if (found)
                        heads.Add(pendingEnumerator);
                    else
                        await pendingEnumerator.DisposeAsync();
                    pendingEnumerator = null;
                }

            }
            finally
            {
                trace?.AddLookup(timer.ElapsedTicks);
                if (pendingEnumerator != null)
                    await pendingEnumerator.DisposeAsync();
                foreach (var enumerator in heads)
                    await enumerator.DisposeAsync();
            }
        }
    }
}
