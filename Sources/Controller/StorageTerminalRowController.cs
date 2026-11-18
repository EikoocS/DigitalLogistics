using DigitalLogistics.Storage.Items;
using DigitalLogistics.Storage;
using DigitalLogistics.UI;

namespace DigitalLogistics.Controller
{
    internal sealed class StorageTerminalRowController
    {
        internal StorageTerminalRow View { get; set; }
        internal StorageTerminalController Owner { get; }
        internal CachedItem Item { get; set; }
        internal StorageTerminalGroup Group { get; set; }

        internal StorageTerminalRowController(StorageTerminalController owner, CachedItem item)
        {
            Owner = owner;
            Item = item;
        }

        public bool HasTooltip() => Group != null || Item?.VerifyValidity() == true;

        public RichText GetTooltip() => Group == null
            ? Item.GetTooltip()
            : View.CreateGroupTooltip(Group.Name, Group.Items.Count, Group.TotalPrice);

        public RichText GetTooltipAdvanced() => Group == null
            ? Item.GetTooltipAdvanced() : GetTooltip();
    }
}
