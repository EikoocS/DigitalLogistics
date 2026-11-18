using DigitalLogistics.Storage;
using DigitalLogistics.Controller;
using HarmonyLib;

namespace DigitalLogistics.Patch
{
    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.ResetData))]
    internal static class StorageSessionReset
    {
        private static void Prefix()
        {
            BusController.ClearConnections();
            StorageDiskStore.Clear();
        }
    }

    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.SaveGame))]
    internal static class StorageSave
    {
        private static void Prefix()
        {
            StorageDiskStore.Save();
            if (!DigitalLogistics.Instance.Enabled) return;
            BusManager.Save();
            IOBusManager.Save();
            TriggerBusManager.Save();
        }
    }
}
