using System;
using DigitalLogistics.Filtering;
using DigitalLogistics.Storage.Disks;
using Newtonsoft.Json.Linq;
using static RenderHandler.ColorPalette;

namespace DigitalLogistics.Item
{
    internal sealed class ItemDisk : GameItemElement
    {
        internal const string ItemId = "item_disk";
        private const string DiskIdTag = "DIGITAL_LOGISTICS_DISK_UUID_STRING";
        private const string CapacityLabel = "1k";
        private const int StorageCapacity = 1024;
        private static readonly StoredItemExpression AllowAll = StoredItemExpression.Constant(true);
        private bool advancedTooltip;

        internal Guid? DiskId => state.dict.TryGetValue(DiskIdTag, out var tag)
            && Guid.TryParse(tag.GetString(), out var id) ? id : null;
        internal int Capacity => StorageCapacity;
        internal string DiskName => CustomDiskName(ReadStorage()) ?? GetDisplayName();
        internal StoredItemExpression Filter
        {
            get
            {
                string filter = Configuration(ReadStorage())?.Value<string>("filter");
                return string.IsNullOrWhiteSpace(filter) ? AllowAll : StoredItemExpressionParser.Parse(filter);
            }
        }

        internal static GameItem Create() => new ItemDisk();

        private ItemDisk() : base(ItemId, "Items/itematlas", "unknown")
        {
            string modId = DigitalLogistics.Instance.Manifest.ID;
            SetSpriteAndShapeFromMod(modId, ItemId).SetShape(new GridShapeBuilder().SetDataFill(2, 2));
            name = ModHelper.GetLocalized(modId, $"item.{ItemId}.name", CapacityLabel);
            shortDescription = ModHelper.GetLocalized(modId, $"item.{ItemId}.desc");
            flavorText = ModHelper.GetLocalized(modId, $"item.{ItemId}.flavor");
        }

        internal StorageDiskData GetStorage()
        {
            if (DiskId is Guid id) return StorageDiskStore.Get(id);

            id = Guid.NewGuid();
            var data = new StorageDiskData();
            data.Metadata["type"] = "item";
            StorageDiskStore.Set(id, data);
            ModifyTag(DiskIdTag, tag => tag.SetString(id.ToString("D")), skipValidate: true);
            return data;
        }

        private StorageDiskData ReadStorage() => DiskId is Guid id ? StorageDiskStore.Get(id) : null;

        private static JObject Configuration(StorageDiskData data) => data?.Metadata["config"] as JObject;

        private string CustomDiskName(StorageDiskData data)
        {
            string customName = Configuration(data)?.Value<string>("name");
            return string.IsNullOrWhiteSpace(customName) || customName == GetDisplayName() ? null : customName;
        }

        public override RichText GetTooltipAdvanced()
        {
            advancedTooltip = true;
            try { return base.GetTooltipAdvanced(); }
            finally { advancedTooltip = false; }
        }

        internal static void AddTooltip(RichTextBuilder builder, GameItem item)
        {
            if (item is not ItemDisk disk || disk.DiskId is not Guid id) return;

            var data = StorageDiskStore.Get(id);
            string customName = disk.CustomDiskName(data);
            if (customName != null) builder.AddLine(customName);
            string uuid = id.ToString("D");
            int middle = uuid.Length / 2;
            builder.AddLine(uuid[..middle]);
            if (disk.advancedTooltip) builder.AddLine(uuid[middle..]);

            int used = data?.UsedCells ?? 0;
            double ratio = (double)used / disk.Capacity;
            var color = ratio >= 0.9 ? LightRed : ratio >= 0.75 ? LightYellow : LightGreen;
            builder.Add($"{used:N0}", color).AddLine($"/{disk.Capacity:N0}");
        }
    }
}
