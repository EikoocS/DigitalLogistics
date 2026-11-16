using DigitalStorage.Storage;
using HarmonyLib;

namespace DigitalStorage.Patch
{
    [HarmonyPatch(typeof(HopperHelper), nameof(HopperHelper.RunWindow))]
    internal static class IOBusCycle
    {
        private static void Postfix(int window)
        {
            if (DigitalStorage.Instance.Enabled) IOBusManager.RunWindow(window);
        }
    }
}
