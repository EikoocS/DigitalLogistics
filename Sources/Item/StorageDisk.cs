using System;
using DigitalLogistics.Filtering;
using DigitalLogistics.Storage.Disks;
using Newtonsoft.Json.Linq;
using static RenderHandler.ColorPalette;

namespace DigitalLogistics.Item
{
    internal abstract class StorageDisk : GameItemElement
    {
        private const string DiskIdTag = "DIGITAL_LOGISTICS_DISK_UUID_STRING";
        private const string CapacityLabel = "1k";
        private const int StorageCapacity = 1024;
        private static readonly CachedItemExpression AllowAll = CachedItemExpression.Constant(true);
        private bool advancedTooltip;

        protected abstract string StorageType { get; }
        protected abstract double GetUsedCapacity(StorageDiskData data);
        protected virtual string CapacityFormat => "N0";
        protected virtual string CapacityUnit => string.Empty;
        protected virtual void InitializeStorage(StorageDiskData data) { }

        internal Guid? DiskId => state.dict.TryGetValue(DiskIdTag, out var tag)
            && Guid.TryParse(tag.GetString(), out var id) ? id : null;
        internal int Capacity => StorageCapacity;
        internal string DiskName => CustomDiskName(ReadStorage()) ?? GetDisplayName();
        internal CachedItemExpression Filter
        {
            get
            {
                string filter = Configuration(ReadStorage())?.Value<string>("filter");
                return string.IsNullOrWhiteSpace(filter) ? AllowAll : CachedItemExpressionParser.Parse(filter);
            }
        }

        protected StorageDisk(string itemId) : base(itemId, "Items/itematlas", "unknown")
        {
            string modId = DigitalLogistics.Instance.Manifest.ID;
            SetSpriteAndShapeFromMod(modId, ItemDisk.ItemId).SetShape(new GridShapeBuilder().SetDataFill(2, 2));
            name = ModHelper.GetLocalized(modId, $"item.{itemId}.name", CapacityLabel);
            shortDescription = ModHelper.GetLocalized(modId, $"item.{itemId}.desc");
            flavorText = ModHelper.GetLocalized(modId, $"item.{itemId}.flavor");
        }

        internal StorageDiskData GetStorage()
        {
            if (DiskId is Guid id) return StorageDiskStore.Get(id);

            id = Guid.NewGuid();
            var data = new StorageDiskData();
            data.Metadata["type"] = StorageType;
            InitializeStorage(data);
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
            if (item is not StorageDisk disk || disk.DiskId is not Guid id) return;

            var data = StorageDiskStore.Get(id);
            string customName = disk.CustomDiskName(data);
            if (customName != null) builder.AddLine(customName);
            string uuid = id.ToString("D");
            int middle = uuid.Length / 2;
            builder.AddLine(uuid[..middle]);
            if (disk.advancedTooltip) builder.AddLine(uuid[middle..]);

            double used = disk.GetUsedCapacity(data);
            double ratio = (double)used / disk.Capacity;
            var color = ratio >= 0.9 ? LightRed : ratio >= 0.75 ? LightYellow : LightGreen;
            builder.Add(used.ToString(disk.CapacityFormat), color)
                .AddLine($"/{disk.Capacity:N0}{disk.CapacityUnit}");
        }
    }
}
