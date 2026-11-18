using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DigitalLogistics.Filtering;
using DigitalLogistics.Storage.Indexing;
using DigitalLogistics.Storage.Items;

namespace DigitalLogistics.Storage
{
    public abstract class StorageProvider : IDisposable
    {
        public int Priority { get; set; }
        public string Name { get; set; } = "Unnamed";
        public StoredItemExpression Filter { get; set; } = StoredItemExpression.Constant(true);
        private StorageIndex storageIndex = new();
        private StorageIndex rebuildingIndex;
        public int Size => storageIndex.Size;
        public bool Disposed { get; private set; }
        public event Action Changed;

        protected StorageProvider() => storageIndex.onChange += NotifyChanged;
        protected void NotifyChanged() => Changed?.Invoke();

        protected void Upsert(CachedItem item)
        {
            storageIndex.Remove(item.UniqueId);
            storageIndex.Add(item);
            rebuildingIndex?.Remove(item.UniqueId);
            rebuildingIndex?.Add(item);
        }

        protected void Remove(int uniqueId)
        {
            storageIndex.Remove(uniqueId);
            rebuildingIndex?.Remove(uniqueId);
        }

        private void ReplaceIndex(StorageIndex index)
        {
            storageIndex.onChange -= NotifyChanged;
            storageIndex = index;
            storageIndex.onChange += NotifyChanged;
            NotifyChanged();
        }

        public void Dispose()
        {
            Disposed = true;
            storageIndex.onChange -= NotifyChanged;
            DisposeCore();
        }

        protected abstract void DisposeCore();
        public abstract bool Contains(int uniqueId);
        internal CachedItem FindIndexedItem(int uniqueId) => Disposed ? null : storageIndex.Find(uniqueId);
        protected abstract IEnumerable<CachedItem> ReadItems();

        public void Refresh()
        {
            var work = RefreshCoroutine(null);
            try { while (work.MoveNext()) { } }
            finally { (work as IDisposable)?.Dispose(); }
        }

        public IEnumerator RefreshCoroutine(Action onItemProcessed)
        {
            if (Disposed) yield break;
            rebuildingIndex = new StorageIndex();
            try
            {
                foreach (var item in ReadItems())
                {
                    if (Disposed) yield break;
                    rebuildingIndex.Remove(item.UniqueId);
                    rebuildingIndex.Add(item);
                    onItemProcessed?.Invoke();
                    yield return null;
                }
                if (!Disposed) ReplaceIndex(rebuildingIndex);
            }
            finally { rebuildingIndex = null; }
        }

        internal StorageInputResult TryInput(StorageOutput output, int amount, bool applyFilter)
        {
            if (Disposed || applyFilter && !Filter.Evaluate(new PhysicalItem(output.Item)))
                return StorageInputResult.Empty;
            amount = Math.Min(amount, output.Item.MaxNumRemove());
            return amount > 0 ? InputCore(output, amount) : StorageInputResult.Empty;
        }

        protected abstract StorageInputResult InputCore(StorageOutput output, int amount);

        internal IReadOnlyList<CachedItem> Snapshot()
        {
            var items = new List<CachedItem>();
            using var enumerator = storageIndex.GetEnumerator(StorageSort.SortBy.IDENTIFIER, false);
            while (enumerator.MoveNext()) items.Add(enumerator.Current);
            return items;
        }

        public async IAsyncEnumerable<CachedItem> SearchAsync(
            StoredItemExpression expression,
            StorageSort.SortBy sortBy = StorageSort.SortBy.IDENTIFIER,
            bool reverse = false,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filteredExpression = expression.And(Filter);
            if (Disposed || !storageIndex.MayMatch(filteredExpression)) yield break;
            var timer = Stopwatch.StartNew();
            using var enumerator = storageIndex.GetEnumerator(sortBy, reverse);
            while (enumerator.MoveNext())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = enumerator.Current;
                if (filteredExpression.Evaluate(item))
                {
                    yield return item;
                    timer.Restart();
                }
                if (timer.Elapsed.TotalMilliseconds >= DigitalLogistics.Instance.Settings.SearchBudgetMilliseconds.Value)
                {
                    await Task.Yield();
                    timer.Restart();
                }
            }
        }
    }
}
