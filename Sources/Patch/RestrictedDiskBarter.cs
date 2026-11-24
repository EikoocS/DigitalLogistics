using DigitalLogistics.Item;
using HarmonyLib;

namespace DigitalLogistics.Patch
{
    [HarmonyPatch(typeof(GameCharacterItem), "BarterInit")]
    internal static class RestrictedDiskBarter
    {
        private static void Prefix(GameCharacterItem __instance, out bool __state) =>
            __state = __instance.barterSellInventory == null;

        private static void Postfix(GameCharacterItem __instance, bool __state)
        {
            if (!__state) return;
            var sellInventory = __instance.barterSellInventory;
            var mayAdd = sellInventory.mayInventoryAddItemFunc;
            sellInventory.mayInventoryAddItemFunc = (item, inventory) =>
                item is not StorageDisk { IsResaleRestricted: true }
                && GraphUtils.FindChildType<StorageDisk>(item, disk => disk.IsResaleRestricted) == null
                && (mayAdd?.InvokeAllReduce(item, inventory, InvokeFuncExtensions.BoolReduceOR) ?? true);
        }
    }
}
