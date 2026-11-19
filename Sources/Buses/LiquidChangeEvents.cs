using System;
using System.Collections.Generic;
using System.Linq;

namespace DigitalLogistics.Buses
{
    internal static class LiquidChangeEvents
    {
        private static readonly Dictionary<GameItem, Action<bool>> listeners = new();

        internal static void Subscribe(GameItem item, Action<bool> changed)
        {
            listeners.TryGetValue(item, out var current);
            listeners[item] = current + changed;
        }

        internal static void Unsubscribe(GameItem item, Action<bool> changed)
        {
            var remaining = listeners[item] - changed;
            if (remaining == null) listeners.Remove(item);
            else listeners[item] = remaining;
        }

        internal static bool IsWatchedPart(GameItem item, string tag) =>
            listeners.ContainsKey(item) && Liquid.Liquids.Any(liquid => liquid.currentPart == tag);

        internal static void Notify(GameItem item, bool increased)
        {
            if (listeners.TryGetValue(item, out var changed)) changed(increased);
        }
    }
}
