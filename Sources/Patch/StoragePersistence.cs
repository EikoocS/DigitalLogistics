using System;
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

    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.EncodeNodes))]
    internal static class StorageTreeEncoding
    {
        [ThreadStatic] private static int depth;
        internal static bool Active => depth > 0;

        private static void Prefix() => depth++;
        private static void Finalizer() => depth--;
    }
}
