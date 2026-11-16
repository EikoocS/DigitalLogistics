using DigitalStorage.Storage;

namespace DigitalStorage.Filtering
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

        public static int ReadInt(CachedItem item, string key) => item.getTag(key)?.GetInt() ?? default;
        public static float ReadFloat(CachedItem item, string key) => item.getTag(key)?.GetFloat() ?? default;
        public static long ReadLong(CachedItem item, string key) => item.getTag(key)?.GetLong() ?? default;
        public static double ReadDouble(CachedItem item, string key) => item.getTag(key)?.GetDouble() ?? default;
        public static bool ReadBool(CachedItem item, string key) => item.getTag(key)?.GetBool() ?? default;
        public static string ReadString(CachedItem item, string key) => item.getTag(key)?.GetString();
    }
}
