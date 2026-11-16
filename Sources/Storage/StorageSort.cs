using System;

namespace DigitalStorage.Storage
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

        public static Func<StoredItem, Object> Getter(this SortBy sortBy)
        {
            switch (sortBy)
            {
                case SortBy.IDENTIFIER: return (StoredItem item) => item.Identifier;
                case SortBy.MOD_ID: return (StoredItem item) => item.ModId;
                case SortBy.DISPLAY_NAME: return (StoredItem item) => item.DisplayName;
                case SortBy.PRICE: return (StoredItem item) => item.Price;
                case SortBy.BASE_PRICE: return (StoredItem item) => item.BasePrice;
                case SortBy.HEAT: return (StoredItem item) => item.Heat;
                default: throw new NotImplementedException();
            }
        }
    }
}
