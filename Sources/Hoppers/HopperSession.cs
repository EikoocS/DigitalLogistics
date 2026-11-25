using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Item;

namespace DigitalLogistics.Hoppers
{
    internal static class HopperSession
    {
        private static readonly HashSet<Hopper> attached = new();
        internal static bool Loading { get; private set; }

        internal static void BeginLoad() => Loading = true;

        internal static void FinishLoad()
        {
            Loading = false;
            foreach (var hopper in attached) hopper.RefreshLinks();
        }

        internal static void Attach(Hopper hopper) => attached.Add(hopper);
        internal static void Detach(Hopper hopper) => attached.Remove(hopper);

        internal static void Save()
        {
            foreach (var hopper in attached)
            {
                hopper.RefreshLinks();
                hopper.Configuration.Save(hopper);
            }
        }

        internal static void Clear()
        {
            foreach (var hopper in attached.ToArray()) hopper.Disconnect();
            Loading = false;
        }
    }
}
