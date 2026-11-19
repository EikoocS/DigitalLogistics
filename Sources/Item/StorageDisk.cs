using System;
using System.Collections.Generic;
using DigitalLogistics.Filtering;
using DigitalLogistics.Storage.Disks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static RenderHandler.ColorPalette;

namespace DigitalLogistics.Item
{
    internal abstract class StorageDisk : GameItemElement
    {
        internal const int SideInCells = 2;

        private enum FillStatus { Green, Yellow, Red }

        private const string DiskIdTag = "DIGITAL_LOGISTICS_DISK_UUID_STRING";
        private static readonly (string Key, int Capacity)[] Tiers =
        {
            ("64", 64), ("256", 256), ("1k", 1 * 1024), ("4k", 4 * 1024),
            ("16k", 16 * 1024), ("64k", 64 * 1024), ("256k", 256 * 1024), ("infinity", 0)
        };
        private static readonly CachedItemExpression AllowAll = CachedItemExpression.Constant(true);
        private readonly string tier;
        private FillStatus fillStatus;

        protected abstract string StorageType { get; }
        protected abstract double GetUsedCapacity(StorageDiskData data);
        protected virtual string CapacityFormat => "N0";
        protected virtual string CapacityUnit => string.Empty;
        protected virtual void InitializeStorage(StorageDiskData data) { }

        internal Guid? DiskId => state.dict.TryGetValue(DiskIdTag, out var tag)
            && Guid.TryParse(tag.GetString(), out var id) ? id : null;
        internal int Capacity { get; }
        private bool IsInfinite => Capacity == 0;
        internal string DiskName => CustomDiskName(ReadStorage()) ?? GetDisplayName();
        internal CachedItemExpression Filter
        {
            get
            {
                string filter = Configuration(ReadStorage())?.Value<string>("filter");
                return string.IsNullOrWhiteSpace(filter) ? AllowAll : CachedItemExpressionParser.Parse(filter);
            }
        }

        protected StorageDisk(string itemId, string familyId, string tier, int capacity)
            : base(itemId, "Items/itematlas", "unknown")
        {
            Capacity = capacity;
            this.tier = tier;
            string modId = DigitalLogistics.Instance.Manifest.ID;
            name = ModHelper.GetLocalized(modId, $"item.{familyId}.name", IsInfinite ? "∞" : tier);
            shortDescription = ModHelper.GetLocalized(modId, $"item.{familyId}.desc");
            flavorText = ModHelper.GetLocalized(modId, $"item.{familyId}.flavor");
            getItemSpriteModifierForItemFunc = item => ((StorageDisk)item).GetAppearanceOverlays();
            SetSpriteAndShapeFromMod(modId, familyId).SetShape(new GridShapeBuilder().SetDataFill(SideInCells, SideInCells));
            onLoaded += item => ((StorageDisk)item).RefreshCapacityStatus();
        }

        internal static void RegisterTiers(ModItemDirectory directory, string familyId,
            Func<string, string, int, GameItem> create)
        {
            foreach (var (tier, capacity) in Tiers)
            {
                string itemId = $"{familyId}_{tier}";
                directory.Add(itemId, () => create(itemId, tier, capacity));
            }
        }

        private List<ItemSpriteModifier> GetAppearanceOverlays()
        {
            var overlays = new List<ItemSpriteModifier>(IsInfinite ? 1 : 2);
            AddIndicators(overlays, "disk", Vector2.zero);
            return overlays;
        }

        internal void AddDriveOverlays(List<ItemSpriteModifier> overlays, Vector2 position)
        {
            overlays.Add(new ItemSpriteModifier(ResolveSpriteByName($"disk_overlay_{StorageType}"), position));
            AddIndicators(overlays, "disk_overlay", position);
        }

        private void AddIndicators(List<ItemSpriteModifier> overlays, string prefix, Vector2 position)
        {
            if (!IsInfinite)
            {
                string status = fillStatus switch
                {
                    FillStatus.Red => "red",
                    FillStatus.Yellow => "yellow",
                    _ => "green"
                };
                overlays.Add(new ItemSpriteModifier(ResolveSpriteByName($"{prefix}_status_{status}"), position, priority: 1));
            }
            overlays.Add(new ItemSpriteModifier(ResolveSpriteByName($"{prefix}_capacity_{tier}"), position, priority: 2));
        }

        private FillStatus GetFillStatus(double used)
        {
            double ratio = used / Capacity;
            return ratio >= 0.9 ? FillStatus.Red : ratio >= 0.75 ? FillStatus.Yellow : FillStatus.Green;
        }

        internal void RefreshCapacityStatus()
        {
            if (IsInfinite) return;
            var status = GetFillStatus(GetUsedCapacity(ReadStorage()));
            if (fillStatus == status) return;
            fillStatus = status;
            Validate();
            if (parentInventory?.GetParentItem() is StorageDrive drive) drive.Validate();
        }

        internal StorageDiskData GetStorage()
        {
            if (IsInfinite) return null;
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

        internal static void AddTooltip(RichTextBuilder builder, GameItem item)
        {
            if (item is not StorageDisk disk || disk.IsInfinite) return;

            var data = disk.ReadStorage();
            string customName = disk.CustomDiskName(data);
            if (customName != null) builder.AddLine(customName);

            double used = disk.GetUsedCapacity(data);
            var color = disk.GetFillStatus(used) switch
            {
                FillStatus.Red => LightRed,
                FillStatus.Yellow => LightYellow,
                _ => LightGreen
            };
            builder.Add(used.ToString(disk.CapacityFormat), color)
                .AddLine($"/{disk.Capacity:N0}{disk.CapacityUnit}");
        }
    }
}
