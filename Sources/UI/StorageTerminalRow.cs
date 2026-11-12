using DigitalStorage.Storage;

namespace DigitalStorage
{
    internal sealed class StorageTerminalRow : EmptyPixelElement, TooltipListener
    {
        internal StorageTerminalList Owner { get; }
        internal StoredItem Item { get; set; }
        internal StorageTerminalGroup Group { get; set; }

        internal StorageTerminalRow(StorageTerminalList owner, StoredItem item)
        {
            Owner = owner;
            Item = item;
        }

        public bool HasTooltip() => Group != null || (Item?.IsAvailable == true
            && Item.Item is TooltipListener listener && listener.HasTooltip());

        public RichText GetTooltip()
        {
            if (Group == null) return ((TooltipListener)Item.Item).GetTooltip();
            var builder = new RichTextBuilder(60);
            builder.AddLine($"{Group.Name} *{Group.Items.Count}");
            builder.AddLine($"{Group.TotalPrice:N0} {LocHelper.GetLocalizedMechanic("mech_credits")}");
            return builder.Build();
        }

        public RichText GetTooltipAdvanced() => Group == null
            ? ((TooltipListener)Item.Item).GetTooltipAdvanced() : GetTooltip();
    }
}
