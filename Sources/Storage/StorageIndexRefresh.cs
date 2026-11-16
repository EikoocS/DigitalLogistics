using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace DigitalStorage.Storage
{
    internal static class StorageIndexRefresh
    {
        private const double FrameBudgetMilliseconds = 2;
        private static Coroutine coroutine;
        private static IEnumerator work;

        internal static bool Running { get; private set; }
        internal static bool HasResult { get; private set; }
        internal static bool Succeeded { get; private set; }
        internal static long ProcessedItems { get; private set; }
        internal static double ElapsedMilliseconds { get; private set; }
        internal static event Action StateChanged;

        internal static void Start()
        {
            if (Running) return;
            Running = true;
            HasResult = false;
            Succeeded = false;
            ProcessedItems = 0;
            work = Run();
            coroutine = ModHelper.StartCoroutine(DigitalStorage.Instance.Manifest.ID, work);
            StateChanged?.Invoke();
        }

        internal static void Clear()
        {
            if (coroutine != null) ModHelper.StopCoroutine(DigitalStorage.Instance.Manifest.ID, coroutine);
            (work as IDisposable)?.Dispose();
            work = null;
            coroutine = null;
            Running = false;
            HasResult = false;
        }

        private static IEnumerator Run()
        {
            var timer = Stopwatch.StartNew();
            var jobs = new List<IEnumerator>();
            try
            {
                foreach (var provider in BusManager.Providers.Concat(DigitalStorage.Manager.Providers).Distinct().ToArray())
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
                    while (jobs.Count > 0 && frameTimer.Elapsed.TotalMilliseconds < FrameBudgetMilliseconds);
                    if (jobs.Count > 0) yield return null;
                }
                Succeeded = true;
            }
            finally
            {
                foreach (var job in jobs) (job as IDisposable)?.Dispose();
                ElapsedMilliseconds = timer.Elapsed.TotalMilliseconds;
                HasResult = true;
                Running = false;
                coroutine = null;
                work = null;
                StateChanged?.Invoke();
            }
        }
    }
}
