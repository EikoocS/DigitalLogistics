using System;

namespace DigitalLogistics.Storage.Items
{
    public abstract class CachedItem
    {
        public int UniqueId { get; protected set; } = 0;
        public string Identifier { get; protected set; } = string.Empty;
        public string ModId { get; protected set; } = string.Empty;
        public string DisplayName { get; protected set; } = string.Empty;
        public ItemTypeMask TypeMask { get; protected set; } = new();
        public long Price { get; protected set; } = -1;
        public long BasePrice { get; protected set; } = -1;
        public int? Heat { get; protected set; } = null;
        public int ContrabandLevel { get; protected set; } = 0;

        protected void CacheProperties(GameItem item)
        {
            UniqueId = item.GetUniqueID();
            Identifier = item.identifier;
            ModId = item.GetModId() ?? string.Empty;
            DisplayName = item.GetDisplayName(preferCustomName: true);
            TypeMask.SetBitByTags(item.GetGameItemType());
            Price = item.GetCurrentValue();
            BasePrice = item.GetBaseValue();
            Heat = StolenHelper.IsStolenItem(item) ? StolenHelper.GetFoundChance(item) : null;
            ContrabandLevel = ContrabandHelper.GetContrabandLevel(item);
        }

        public abstract ItemInstanceLease AcquireInstance();
        public abstract bool VerifyValidity();

        public RichText GetTooltip() => ReadItem(item =>
            item is TooltipListener listener && listener.HasTooltip()
                ? listener.GetTooltip() : new RichTextBuilder().Build());

        public RichText GetTooltipAdvanced() => ReadItem(item =>
            item is TooltipListener listener && listener.HasTooltip()
                ? listener.GetTooltipAdvanced() : new RichTextBuilder().Build());

        public virtual TagState GetTag(string key) => ReadItem(item =>
            item.modifiedState.dict.TryGetValue(key, out var tag) ? tag?.Clone() : null);

        public virtual long LiquidVolume => ReadItem(WaterHelper.GetTotalVolume);
        public virtual long FreeLiquidVolume => ReadItem(item =>
            (long)item.GetTagReadonly(WaterHelper.LIQUID_CONTAINER_CAPACITY).GetInt() - WaterHelper.GetTotalVolume(item));
        internal virtual long ReadLiquidAmount(string key) => GetTag(key)?.GetInt() ?? 0;

        private T ReadItem<T>(Func<GameItem, T> read)
        {
            using var lease = AcquireInstance();
            return lease == null ? default : read(lease.Item);
        }

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
