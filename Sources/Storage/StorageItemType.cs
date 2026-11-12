using System;
using System.Collections.Generic;

namespace DigitalStorage.Storage
{
    [Flags]
    public enum StorageItemType : ulong
    {
        NONE = 0,
        STOLEN_TAG = 1UL << 0,
        FOOD = 1UL << 1,
        MEDICAL = 1UL << 2,
        MATERIAL = 1UL << 3,
        HOUSEHOLD_GOOD = 1UL << 4,
        SUBSTANCE = 1UL << 5,
        WEAPON = 1UL << 6,
        NOT_FOR_RESALE = 1UL << 7,
        SUPPLY_CRATE = 1UL << 8,
        TOOL = 1UL << 9,
        MEDICAL_TOOL = 1UL << 10,
        CONTRABAND = 1UL << 11,
        RESTRICTED_1 = 1UL << 12,
        RESTRICTED_2 = 1UL << 13,
        RESTRICTED_3 = 1UL << 14,
        MAKESHIFT = 1UL << 15,
        LOWTECH = 1UL << 16,
        MIDLINE = 1UL << 17,
        HIGHEND = 1UL << 18,
        RARE = 1UL << 19,
        LUXURY_ITEM = 1UL << 20,
        ART = 1UL << 21,
        WATER = 1UL << 22,
        WATER_RATION = 1UL << 23,
        FARMING_SUPPLIES = 1UL << 24,
        CHEMICAL_SUPPLIES = 1UL << 25,
        FRESH_FOOD = 1UL << 26,
        PROCESSED_FOOD = 1UL << 27,
        TREAT = 1UL << 28,
        BREVAGE = 1UL << 29,
        DOCUMENT = 1UL << 30,
        ACCESS_CARD = 1UL << 31,
        SEED = 1UL << 32,
        MODIFIED_SEED = 1UL << 33,
        BASIC_MATERIAL = 1UL << 34,
        REFINED_MATERIAL = 1UL << 35,
        ENERGY_CELL = 1UL << 36,
        NARCOTIC = 1UL << 37,
        POISON = 1UL << 38,
        STORAGE = 1UL << 39,
        WATER_PURIFICATION_SUPPLY = 1UL << 40,
        OXYGEN_SUPPLY = 1UL << 41,
        ALCOHOL = 1UL << 42,
        MELEE_WEAPON = 1UL << 43,
        FIREARM = 1UL << 44,
        MODDABLE = 1UL << 45,
        AMMUNITION = 1UL << 46,
        MUNITION = 1UL << 47,
        GUN_MOD = 1UL << 48,
        MODULE = 1UL << 49,
        NODE = 1UL << 50,
        HYDROPONIC_MODULE = 1UL << 51,
        ALARM_MODULE = 1UL << 52,
        PRINTER_MODULE = 1UL << 53,
        EVAPORATOR_MODULE = 1UL << 54,
        MACHINE = 1UL << 55,
        ORGAN = 1UL << 56,
        ARMOR = 1UL << 57,
        FILTER = 1UL << 58,
        LOTTERY = 1UL << 59,
        JUNK = 1UL << 60,
    }

    public static class StorageItemTypeHelper
    {
        private static readonly Dictionary<string, ulong> tagMasks = CreateTagMasks();
        private static readonly List<(string Name, ulong Mask)> localizedTagIndex = new();
        private static string indexedLocale;

        public static ulong FromTag(string tag) => tagMasks[tag];

        public static ulong FromTags(IEnumerable<string> tags)
        {
            ulong mask = 0;
            foreach (var tag in tags) mask |= FromTag(tag);
            return mask;
        }

        public static ulong FromLocalizedName(string keyword)
        {
            string locale = LocHelper.GetCurrentLocaleCode();
            if (indexedLocale != locale)
            {
                localizedTagIndex.Clear();
                foreach (var entry in tagMasks)
                {
                    localizedTagIndex.Add((GetLocalizedName(entry.Key), entry.Value));
                }
                indexedLocale = locale;
            }

            ulong mask = 0;
            foreach (var entry in localizedTagIndex)
                if (entry.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    mask |= entry.Mask;
            return mask;
        }

        public static List<string> ToTags(ulong mask)
        {
            var tags = new List<string>();
            foreach (var entry in tagMasks)
                if ((mask & entry.Value) != 0) tags.Add(entry.Key);
            return tags;
        }

        public static ulong GetUnsafeMask() => (ulong)StorageItemType.STOLEN_TAG | (ulong)StorageItemType.CONTRABAND;

        public static string GetLocalizedName(string tag) =>
            tag == nameof(StorageItemType.STOLEN_TAG)
                ? LocHelper.GetLocalizedMechanic("mech_stolen_tooltip", 0)
                : TypeHelper.GetTypeDisplayName(tag);

        private static Dictionary<string, ulong> CreateTagMasks()
        {
            var masks = new Dictionary<string, ulong>(StringComparer.Ordinal);
            foreach (StorageItemType type in Enum.GetValues(typeof(StorageItemType)))
                if (type != StorageItemType.NONE) masks.Add(type.ToString(), (ulong)type);
            return masks;
        }
    }
}

