using DigitalLogistics.Storage;
using HarmonyLib;

namespace DigitalLogistics.Patch
{
    [HarmonyPatch(typeof(ItemPinHelper), nameof(ItemPinHelper.SetPinned))]
    internal static class StorageItemPin
    {
        private static void Prefix(GameItem item, out bool __state) => __state = ItemPinHelper.IsPinned(item);

        private static void Postfix(GameItem item, bool __state)
        {
            if (item == null || __state == ItemPinHelper.IsPinned(item)) return;
            SharedProviderRegistry.OnPinChanged(item);
            DigitalLogistics.Manager?.NotifyChanged();
        }
    }
}
