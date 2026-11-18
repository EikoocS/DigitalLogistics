using HarmonyLib;

namespace DigitalLogistics.Patch
{
    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.ResetData))]
    internal static class StorageSessionReset
    {
        private static void Prefix() => LogisticsSession.Reset();
    }

    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.SaveGame))]
    internal static class StorageSave
    {
        private static void Prefix() => LogisticsSession.Save();
    }
}
