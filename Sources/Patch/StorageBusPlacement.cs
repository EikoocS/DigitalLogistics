using DigitalLogistics.Item;
using HarmonyLib;

namespace DigitalLogistics.Patch
{
    [HarmonyPatch(typeof(HopperHelper), nameof(HopperHelper.MayBypassGridRules))]
    internal static class StorageBusPlacement
    {
        private static void Postfix(GameItem item, GameInventory inv, ref bool __result)
        {
            if (Bus.IsBus(item) && GeneralHelper.IsItemOwned(item))
                __result = inv == null || !inv.IsTag(HopperHelper.NO_HOPPER_INVENTORY_TAG);
        }
    }
}
