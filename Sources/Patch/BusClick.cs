using DigitalStorage.Item;
using HarmonyLib;

namespace DigitalStorage.Patch
{
    [HarmonyPatch(typeof(ItemMouseDoubleClickHandler), "ClickAction")]
    internal static class BusClick
    {
        private static void Prefix(GameItem newItem, ref bool activate) =>
            Bus.SuppressClickActivation(newItem, ref activate);
    }
}
