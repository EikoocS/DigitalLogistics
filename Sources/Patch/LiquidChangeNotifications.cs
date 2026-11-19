using System;
using DigitalLogistics.Buses;
using HarmonyLib;

namespace DigitalLogistics.Patch
{
    [HarmonyPatch(typeof(GameItem), nameof(GameItem.ModifyTag),
        new[] { typeof(string), typeof(Action<TagState>), typeof(bool) })]
    internal static class LiquidChangeNotifications
    {
        private static void Prefix(GameItem __instance, string tagName, Action<TagState> action, out int? __state)
        {
            __state = action != null && LiquidChangeEvents.IsWatchedPart(__instance, tagName)
                ? (__instance.state.dict.TryGetValue(tagName, out var tag) ? tag.GetInt() : 0) : null;
        }

        private static void Postfix(GameItem __instance, string tagName, int? __state)
        {
            if (__state.HasValue && __instance.state.dict[tagName].GetInt() != __state.Value)
                LiquidChangeEvents.Notify(__instance, __instance.state.dict[tagName].GetInt() > __state.Value);
        }
    }
}
