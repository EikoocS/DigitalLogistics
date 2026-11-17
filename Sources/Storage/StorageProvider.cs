using DigitalLogistics.Filtering;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalLogistics.Storage
{
    public abstract class StorageProvider : IDisposable
    {
        public int Priority { get; set; } = 0;
        public string Name { get; set; } = "Unnamed";
        public StoredItemExpression filter { get; set; } = StoredItemExpression.Constant(true);
        protected StorageIndex storageIndex = new();
        public int Size => storageIndex.Size;
        public event Action onChange;

        protected StorageProvider() => storageIndex.onChange += NotifyChanged;

        protected void NotifyChanged() => onChange?.Invoke();

        protected void ReplaceIndex(StorageIndex index)
        {
            storageIndex.onChange -= NotifyChanged;
            storageIndex = index;
            storageIndex.onChange += NotifyChanged;
            NotifyChanged();
        }

        public abstract void Dispose();
        public abstract bool Contains(int uniqueId);
        public abstract void Refresh();
        public abstract IEnumerator RefreshCoroutine(Action onItemProcessed);

        /// <summary>Returns the actual inserted count and the resulting destination entries.</summary>
        public StorageInputResult TryInput(GameItem item, int amount = -1)
        {
            if (!filter.Evaluate(new PhysicalItem(item))) return StorageInputResult.Empty;
            amount = amount < 0 ? item.unitCount : Math.Min(amount, item.unitCount);
            return amount > 0 ? InputCore(item, amount) : StorageInputResult.Empty;
        }

        protected abstract StorageInputResult InputCore(GameItem item, int amount);

        internal IReadOnlyList<CachedItem> Snapshot()
        {
            var items = new List<CachedItem>();
            using var enumerator = storageIndex.GetEnumerator(StorageSort.SortBy.IDENTIFIER, false);
            while (enumerator.MoveNext()) items.Add(enumerator.Current);
            return items;
        }

        /// <summary>Place and complete each output before advancing the enumerator.</summary>
        public async IAsyncEnumerable<StorageOutput> PrepareOutputAsync(    
            StoredItemExpression expression,
            StorageSort.SortBy sortBy = StorageSort.SortBy.IDENTIFIER,
            bool reverse = false,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!PrepareSearch(expression)) yield break;

            await foreach (var item in SearchAsync(expression, sortBy, reverse, cancellationToken))
            {
                using var output = item.PrepareOutput();
                if (output != null) yield return output;
            }
        }

        // false to skip
        public bool PrepareSearch(StoredItemExpression expression)
        {
            var plan = new StoredItemFilterBuilder()
                .AddExpression(expression)
                .AddExpression(filter)
                .WithIndexBlooms(storageIndex.GetIndexBloom())
                .WithTypeBloom(storageIndex.GetTypeBloom())
                .Build();
            return plan.MayMatch();
        }
        public async IAsyncEnumerable<CachedItem> SearchAsync(
            StoredItemExpression expression,
            StorageSort.SortBy sortBy = StorageSort.SortBy.IDENTIFIER,
            bool reverse = false,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var timer = Stopwatch.StartNew();
            var enumerator = storageIndex.GetEnumerator(sortBy, reverse);
            var filteredExpression = expression.And(filter);
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
