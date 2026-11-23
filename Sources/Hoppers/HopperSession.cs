using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Item;
using UnityEngine;

namespace DigitalLogistics.Hoppers
{
    internal static class HopperSession
    {
        private static readonly HashSet<Hopper> attached = new();
        private static readonly HashSet<Hopper> pending = new();
        private static Coroutine scheduled;
        private static bool draining;
        internal static bool Loading { get; private set; }
        internal static bool CanRun => DigitalLogistics.Instance.Enabled && !Loading && !SaveManager.IsDecoding
            && !(MapUIManager.Instance != null && MapUIManager.Instance.IsOutside);

        internal static void BeginLoad() => Loading = true;

        internal static void FinishLoad()
        {
            Loading = false;
            foreach (var hopper in attached) hopper.RefreshLinks();
        }

        internal static void Attach(Hopper hopper) => attached.Add(hopper);
        internal static void Cancel(Hopper hopper) => pending.Remove(hopper);

        internal static void Detach(Hopper hopper)
        {
            attached.Remove(hopper);
            Cancel(hopper);
        }

        internal static void Schedule(Hopper hopper)
        {
            if (!CanRun) return;
            pending.Add(hopper);
            if (!draining && scheduled == null)
                scheduled = ModHelper.StartCoroutine(DigitalLogistics.Instance.Manifest.ID, RunPending());
        }

        private static IEnumerator RunPending()
        {
            yield return null;
            scheduled = null;
            draining = true;
            var executed = new HashSet<Hopper>();
            try
            {
                while (pending.Count != 0)
                {
                    var batch = pending.ToArray();
                    foreach (var hopper in batch)
                        if (pending.Remove(hopper) && executed.Add(hopper)) hopper.TryTransfer();
                }
            }
            finally
            {
                pending.Clear();
                draining = false;
            }
        }

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
            if (scheduled != null) ModHelper.StopCoroutine(DigitalLogistics.Instance.Manifest.ID, scheduled);
            scheduled = null;
            foreach (var hopper in attached.ToArray()) hopper.Disconnect();
            Loading = false;
        }
    }
}
