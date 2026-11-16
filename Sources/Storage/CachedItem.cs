namespace DigitalStorage.Storage
{
    public abstract class CachedItem
    {
        // The following content should be cached in CachedItem, not GameItem.
        public int UniqueId { get; protected set; } = 0;
        public string Identifier { get; protected set; } = string.Empty;
        public string ModId { get; protected set; } = string.Empty;
        public string DisplayName { get; protected set; } = string.Empty;
        public ItemTypeMask TypeMask { get; protected set; } = new();
        public long Price { get; protected set; } = -1;
        public long BasePrice { get; protected set; } = -1;
        public int? Heat { get; protected set; } = null;
        public int ContrabandLevel { get; protected set; } = 0;

        // Does GetItemInstance return the item in the inventory or the virtualized item
        public abstract bool IsDigitalItems();

        // Get a GameItem instance; if it's a virtual item, create it.
        public abstract GameItem GetItemInstance();

        public abstract StorageOutput PrepareOutput();

        // Verify the validity of the CachedItem, without applying the cache.
        public abstract bool VerifyValidity();

        // obtaining ToolTip, Maybe temporary deserialization
        public abstract RichText GetTooltip();

        // At the time of writing this code, this functionality had no actual impact.
        public abstract RichText GetTooltipAdvanced();

        // Get Item Tag, Return a clone
        public abstract TagState getTag(string key);

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((CachedItem)obj);
        }

        public bool Equals(CachedItem other)
        {
            return UniqueId == other.UniqueId;
        }

        public override int GetHashCode() => UniqueId.GetHashCode();
    }
}
