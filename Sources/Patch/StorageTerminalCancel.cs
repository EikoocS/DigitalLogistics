using DigitalLogistics.Controller;
using HarmonyLib;

namespace DigitalLogistics.Patch
{
    [HarmonyPatch(typeof(StoreUIManager), nameof(StoreUIManager.ResolveEscape))]
    internal static class StorageTerminalCancel
    {
        private static bool Prefix(ref bool __result)
        {
            if (!DigitalLogistics.Instance.Enabled || !StorageTerminalInteraction.TryCancelHeldItem()) return true;
            __result = true;
            return false;
        }
    }
}
