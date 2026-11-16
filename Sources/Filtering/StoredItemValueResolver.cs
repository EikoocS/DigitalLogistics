using DigitalStorage.Storage;

namespace DigitalStorage.Filtering
{
    internal static class StoredItemValueResolver
    {
        public static string ReadIdentifier(StoredItem item) => item.Identifier;
        public static string ReadModId(StoredItem item) => item.ModId;
        public static string ReadDisplayName(StoredItem item) => item.DisplayName;
        public static long ReadPrice(StoredItem item) => item.Price;
        public static long ReadBasePrice(StoredItem item) => item.BasePrice;
        public static int ReadHeat(StoredItem item) => item.Heat ?? 0;
        public static int ReadContrabandLevel(StoredItem item) => item.ContrabandLevel;
        public static bool ReadItemType(StoredItem item, int index) => item.TypeMask[index];

        public static int ReadInt(StoredItem item, string key) => item.getTag(key)?.GetInt() ?? default;
        public static float ReadFloat(StoredItem item, string key) => item.getTag(key)?.GetFloat() ?? default;
        public static long ReadLong(StoredItem item, string key) => item.getTag(key)?.GetLong() ?? default;
        public static double ReadDouble(StoredItem item, string key) => item.getTag(key)?.GetDouble() ?? default;
        public static bool ReadBool(StoredItem item, string key) => item.getTag(key)?.GetBool() ?? default;
        public static string ReadString(StoredItem item, string key) => item.getTag(key)?.GetString();
    }
}
