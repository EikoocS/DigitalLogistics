using System.Collections.Generic;
using DigitalStorage.Collections;
using DigitalStorage.Storage;
using static DigitalStorage.Storage.StorageSort;

namespace DigitalStorage.Filtering
{
    public sealed class StoredItemFilterBuilder
    {
        private readonly List<StoredItemExpression> expressions = new();
        private readonly Dictionary<SortBy, BloomFilter> blooms = new();
        private BloomFilter typeBloom;

        public StoredItemFilterBuilder AddExpression(string expression) =>
            AddExpression(StoredItemExpressionParser.Parse(expression));

        public StoredItemFilterBuilder AddExpression(StoredItemExpression expression)
        {
            expressions.Add(expression);
            return this;
        }

        public StoredItemFilterBuilder WithBloom(SortBy field, BloomFilter bloom)
        {
            blooms[field] = bloom;
            return this;
        }

        public StoredItemFilterBuilder WithTypeBloom(BloomFilter bloom)
        {
            typeBloom = bloom;
            return this;
        }

        public StoredItemFilterBuilder WithBlooms(StorageIndexBloom indexBloom)
        {
            foreach (var entry in indexBloom.Blooms) blooms[entry.Key] = entry.Value;
            typeBloom = indexBloom.TypeBloom;
            return this;
        }

        public StoredItemFilterPlan Build() => new StoredItemFilterPlan(expressions.ToArray(),
            new Dictionary<SortBy, BloomFilter>(blooms), typeBloom);
    }
}
