using System;
using System.Collections.Generic;
using HarmonyLib;

namespace DigitalLogistics.Patch
{
    [HarmonyPatch(typeof(ItemMouseDoubleClickHandler), "ClickAction")]
    internal static class SuppressItemClickActivation
    {
        private static readonly List<Func<GameItem, bool>> rules = new();

        internal static void Register(Func<GameItem, bool> shouldSuppress) => rules.Add(shouldSuppress);

        private static void Prefix(GameItem newItem, ref bool activate)
        {
            if (!activate) return;
            foreach (var shouldSuppress in rules)
            {
                if (!shouldSuppress(newItem)) continue;
                activate = false;
                return;
            }
        }
    }
}
