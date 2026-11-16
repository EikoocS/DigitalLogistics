using DigitalStorage.Controller;
using HarmonyLib;

namespace DigitalStorage.Patch
{
    [HarmonyPatch(typeof(StoreUIManager), nameof(StoreUIManager.ResolveEscape))]
    internal static class StorageTerminalCancel
    {
        private static bool Prefix(ref bool __result)
        {
            if (!DigitalStorage.Instance.Enabled || !StorageTerminalInteraction.TryCancelHeldItem()) return true;
            __result = true;
            return false;
        }
    }
}
