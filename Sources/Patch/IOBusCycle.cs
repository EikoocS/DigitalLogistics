using HarmonyLib;

namespace DigitalLogistics.Patch
{
    [HarmonyPatch(typeof(HopperHelper), nameof(HopperHelper.RunWindow))]
    internal static class IOBusCycle
    {
        private static void Postfix() => LogisticsScheduler.RunNightWindow();
    }
}
