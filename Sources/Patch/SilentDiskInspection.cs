using DigitalLogistics.Storage.Disks;
using HarmonyLib;

namespace DigitalLogistics.Patch
{
    [HarmonyPatch(typeof(AudioManager), nameof(AudioManager.CanPlay))]
    internal static class SilentDiskInspection
    {
        private static bool Prefix(ref bool __result)
        {
            if (!ItemSerialization.Inspecting) return true;
            __result = false;
            return false;
        }
    }
}
