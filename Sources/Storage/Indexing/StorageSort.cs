using System;
using System.Collections.Generic;
using DigitalLogistics.Collections;
using DigitalLogistics.Storage.Items;

namespace DigitalLogistics.Storage.Indexing
{
    public static class StorageSort
    {
        public enum SortBy
        {
            IDENTIFIER,
            MOD_ID,
            DISPLAY_NAME,
            PRICE,
            BASE_PRICE,
            HEAT
        }

        public static Comparer<CachedItem> Comparer(this SortBy sortBy) => ComparerHelper.ComparedBy(sortBy.Getter());

        public static Func<CachedItem, Object> Getter(this SortBy sortBy)
        {
            switch (sortBy)
            {
                case SortBy.IDENTIFIER: return (CachedItem item) => item.Identifier;
                case SortBy.MOD_ID: return (CachedItem item) => item.ModId;
                case SortBy.DISPLAY_NAME: return (CachedItem item) => item.DisplayName;
                case SortBy.PRICE: return (CachedItem item) => item.Price;
                case SortBy.BASE_PRICE: return (CachedItem item) => item.BasePrice;
                case SortBy.HEAT: return (CachedItem item) => item.Heat;
                default: throw new NotImplementedException();
            }
        }
    }
}
