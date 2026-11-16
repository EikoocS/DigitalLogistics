using DigitalStorage.Storage;
using DigitalStorage.Controller;
using HarmonyLib;

namespace DigitalStorage.Patch
{
    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.ResetData))]
    internal static class StorageBusSession
    {
        private static void Prefix() => StorageBusController.ClearConnections();
    }

    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.SaveGame))]
    internal static class StorageBusSave
    {
        private static void Prefix()
        {
            if (DigitalStorage.Instance.Enabled) StorageBusManager.Save();
        }
    }
}
