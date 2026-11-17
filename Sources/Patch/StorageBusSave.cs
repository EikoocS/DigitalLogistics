using DigitalLogistics.Storage;
using DigitalLogistics.Controller;
using HarmonyLib;

namespace DigitalLogistics.Patch
{
    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.ResetData))]
    internal static class StorageBusSession
    {
        private static void Prefix() => BusController.ClearConnections();
    }

    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.SaveGame))]
    internal static class StorageBusSave
    {
        private static void Prefix()
        {
            if (!DigitalLogistics.Instance.Enabled) return;
            BusManager.Save();
            IOBusManager.Save();
            TriggerBusManager.Save();
        }
    }
}
