using System;

namespace DigitalStorage.Storage
{
    internal class PhysicalStoredItem : StoredItem
    {
        private readonly GameItem item;
        private readonly InventoryStorageProvider provider;
        
        public PhysicalStoredItem(GameItem item, InventoryStorageProvider provider)
        {
            this.item = item;
            this.provider = provider;

            UniqueId = item.GetUniqueID();
            Identifier = item.identifier;
            ModId = item.GetModId();
            DisplayName = item.GetDisplayName(preferCustomName: true);
            TypeMask.SetBitByTags(item.GetGameItemType());
            Price = item.GetCurrentValue();
            BasePrice = item.GetBaseValue();
            Heat = StolenHelper.IsStolenItem(item) ? StolenHelper.GetFoundChance(item) : null;
            ContrabandLevel = ContrabandHelper.GetContrabandLevel(item);
        }

        public override bool IsDigitalItems()
        {
            return false;
        }

        public override GameItem GetItemInstance()
        {
            return item;
        }
        
        public override RichText GetTooltip()
        {
            return item is TooltipListener listener && listener.HasTooltip()
                ? listener.GetTooltip()
                : new RichTextBuilder().Build();
        }

        public override RichText GetTooltipAdvanced()
        {
            return item is TooltipListener listener && listener.HasTooltip()
                ? listener.GetTooltipAdvanced()
                : new RichTextBuilder().Build();
        }

        
        public override bool VerifyValidity()
        {
            return (!provider.Disposed) && provider.Contains(item.uniqueId);
        }

        public override TagState getTag(string key)
        {
            if (!item.modifiedState.dict.TryGetValue(key, out TagState tag) || tag == null)
                return null;
            else 
                return tag.Clone();
        }
    }
}
