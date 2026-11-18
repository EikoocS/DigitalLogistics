using DigitalLogistics.Storage.Items;

namespace DigitalLogistics.Filtering
{
    internal static class StoredItemValueResolver
    {
        public static string ReadIdentifier(CachedItem item) => item.Identifier;
        public static string ReadModId(CachedItem item) => item.ModId;
        public static string ReadDisplayName(CachedItem item) => item.DisplayName;
        public static long ReadPrice(CachedItem item) => item.Price;
        public static long ReadBasePrice(CachedItem item) => item.BasePrice;
        public static int ReadHeat(CachedItem item) => item.Heat ?? 0;
        public static int ReadContrabandLevel(CachedItem item) => item.ContrabandLevel;
        public static bool ReadItemType(CachedItem item, int index) => item.TypeMask[index];

        public static int ReadLiquidVolume(CachedItem item)
        {
            using var lease = item.AcquireInstance();
            return lease == null ? 0 : WaterHelper.GetTotalVolume(lease.Item);
        }

        public static int ReadFreeLiquidVolume(CachedItem item)
        {
            using var lease = item.AcquireInstance();
            return lease == null ? 0 : lease.Item.GetTagReadonly(WaterHelper.LIQUID_CONTAINER_CAPACITY).GetInt()
                - WaterHelper.GetTotalVolume(lease.Item);
        }

        public static bool ReadEnabled(CachedItem item, string key) => item.GetTag(key)?.IsEnabled() ?? false;

        public static int ReadInt(CachedItem item, string key) => item.GetTag(key)?.GetInt() ?? default;
        public static float ReadFloat(CachedItem item, string key) => item.GetTag(key)?.GetFloat() ?? default;
        public static long ReadLong(CachedItem item, string key) => item.GetTag(key)?.GetLong() ?? default;
        public static double ReadDouble(CachedItem item, string key) => item.GetTag(key)?.GetDouble() ?? default;
        public static bool ReadBool(CachedItem item, string key) => item.GetTag(key)?.GetBool() ?? default;
        public static string ReadString(CachedItem item, string key) => item.GetTag(key)?.GetString();
    }
}
