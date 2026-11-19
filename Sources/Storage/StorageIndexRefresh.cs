using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace DigitalLogistics.Storage
{
    internal static class StorageIndexRefresh
    {
        private static Coroutine coroutine;
        private static IEnumerator work;
        private static bool canceled;

        internal static bool Running { get; private set; }
        internal static bool Succeeded { get; private set; }
        internal static long ProcessedItems { get; private set; }
        internal static double ElapsedMilliseconds { get; private set; }
        internal static event Action StateChanged;
        internal static event Action Completed;

        internal static void Start()
        {
            if (Running) return;
            canceled = false;
            Running = true;
            Succeeded = false;
            ProcessedItems = 0;
            work = Run();
            coroutine = ModHelper.StartCoroutine(DigitalLogistics.Instance.Manifest.ID, work);
            StateChanged?.Invoke();
        }

        internal static void Clear()
        {
            canceled = true;
            if (coroutine != null) ModHelper.StopCoroutine(DigitalLogistics.Instance.Manifest.ID, coroutine);
            (work as IDisposable)?.Dispose();
            work = null;
            coroutine = null;
            Running = false;
        }

        private static IEnumerator Run()
        {
            var timer = Stopwatch.StartNew();
            var jobs = new List<IEnumerator>();
            try
            {
                foreach (var provider in SharedProviderRegistry.Providers.Concat(DigitalLogistics.Manager.ItemProviders).Distinct().ToArray())
                    jobs.Add(provider.RefreshCoroutine(() => ProcessedItems++));
                yield return null;
                int index = 0;
                while (jobs.Count > 0)
                {
                    var frameTimer = Stopwatch.StartNew();
                    do
                    {
                        if (jobs[index].MoveNext()) index++;
                        else
                        {
                            (jobs[index] as IDisposable)?.Dispose();
                            jobs.RemoveAt(index);
                        }
                        if (index == jobs.Count) index = 0;
                    }
                    while (jobs.Count > 0 && frameTimer.Elapsed.TotalMilliseconds < DigitalLogistics.Instance.Settings.IndexRefreshBudgetMilliseconds.Value);
                    if (jobs.Count > 0) yield return null;
                }
                Succeeded = true;
            }
            finally
            {
                foreach (var job in jobs) (job as IDisposable)?.Dispose();
                ElapsedMilliseconds = timer.Elapsed.TotalMilliseconds;
                Running = false;
                coroutine = null;
                work = null;
                StateChanged?.Invoke();
                if (!canceled) Completed?.Invoke();
            }
        }
    }
}
