using System.Collections.Generic;
using DigitalStorage.Collections;
using static DigitalStorage.Storage.StorageSort;

namespace DigitalStorage.Filtering
{
    public sealed class StoredItemFilterPlan
    {
        private readonly StoredItemExpression[] expressions;
        private readonly IReadOnlyDictionary<SortBy, BloomFilter> blooms;
        private readonly BloomFilter typeBloom;

        internal StoredItemFilterPlan(StoredItemExpression[] expressions,
            IReadOnlyDictionary<SortBy, BloomFilter> blooms, BloomFilter typeBloom)
        {
            this.expressions = expressions;
            this.blooms = blooms;
            this.typeBloom = typeBloom;
        }

        /// <summary>False proves no match for the AND of all expressions. Bloom instances remain shared with their owner.</summary>
        public bool MayMatch() => StoredItemExpressionAnalyzer.MayMatch(expressions, blooms, typeBloom);
    }
}
