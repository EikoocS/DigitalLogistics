using DigitalStorage.Filtering;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalStorage.Storage
{
    public abstract class StorageProvider : IDisposable
    {
        // TODO CONFIG NEED
        private const double SearchBudgetMilliseconds = 2;
        public string Name { get; protected set; } = "Unnamed";
        public StoredItemExpression filter { get; protected set; } = StoredItemExpression.Constant(true);
        protected readonly StorageIndex storageIndex = new();
        public event Action Changed;

        protected void NotifyChanged() => Changed?.Invoke();
        public abstract void Dispose();
        public abstract bool Contains(int uniqueId);
        public abstract void Refresh();
        // false to skip
        public bool PrepareSearch(StoredItemExpression expression)
        {
            var plan = new StoredItemFilterBuilder()
                .AddExpression(expression)
                .AddExpression(filter)
                .WithBlooms(storageIndex.GetIndexBloom())
                .WithTypeBloom(storageIndex.GetTypeBloom())
                .Build();
            return plan.MayMatch();
        }
        public async IAsyncEnumerable<StoredItem> SearchAsync(
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

                if (timer.Elapsed.TotalMilliseconds >= SearchBudgetMilliseconds)
                {
                    await Task.Yield();
                    timer.Restart();
                }
            }
        }
    }
}
