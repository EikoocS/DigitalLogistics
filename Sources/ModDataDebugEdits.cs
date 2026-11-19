using System.Collections.Generic;

namespace DigitalLogistics
{
    internal static class ModDataDebugEdits
    {
        private static readonly Dictionary<string, string> overrides = new();

        internal static string ModId => DigitalLogistics.Instance.Manifest.ID;

        internal static void Set(string key, string value)
        {
            overrides[key] = value;
            ModHelper.SetModData(ModId, key, value);
        }

        internal static void Remove(string key)
        {
            overrides[key] = null;
            ModHelper.RemoveModData(ModId, key);
        }

        internal static void WriteToSave()
        {
            foreach (var (key, value) in overrides)
            {
                if (value == null) ModHelper.RemoveModData(ModId, key);
                else ModHelper.SetModData(ModId, key, value);
            }
        }

        internal static void Clear() => overrides.Clear();
    }
}
