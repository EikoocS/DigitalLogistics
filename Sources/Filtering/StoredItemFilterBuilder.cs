using System.Collections.Generic;
using DigitalStorage.Collections;
using static DigitalStorage.Storage.StorageSort;

namespace DigitalStorage.Filtering
{
    public sealed class StoredItemFilterBuilder
    {
        private readonly List<StoredItemExpression> expressions = new();
        private readonly Dictionary<SortBy, BloomFilter> indexBlooms = new();
        private BloomFilter typeBloom;

        public StoredItemFilterBuilder AddExpression(string expression) =>
            AddExpression(StoredItemExpressionParser.Parse(expression));

        public StoredItemFilterBuilder AddExpression(StoredItemExpression expression)
        {
            expressions.Add(expression);
            return this;
        }

        public StoredItemFilterBuilder WithIndexBloom(SortBy field, BloomFilter bloom)
        {
            indexBlooms[field] = bloom;
            return this;
        }

        public StoredItemFilterBuilder WithTypeBloom(BloomFilter bloom)
        {
            typeBloom = bloom;
            return this;
        }

        public StoredItemFilterBuilder WithIndexBlooms(Dictionary<SortBy, BloomFilter> indexBlooms)
        {
            foreach (var entry in indexBlooms)
                this.indexBlooms[entry.Key] = entry.Value; 
            return this;
        }

        public StoredItemFilterPlan Build() => new StoredItemFilterPlan(expressions.ToArray(),
            new Dictionary<SortBy, BloomFilter>(indexBlooms), typeBloom);
    }
}
