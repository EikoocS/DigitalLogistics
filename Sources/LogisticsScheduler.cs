using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DigitalLogistics.Tracing;
using UnityEngine;

namespace DigitalLogistics
{
    internal enum TransferDirection { Output, Input, Local }

    internal static class LogisticsScheduler
    {
        private const double ActiveInterval = 0.25;
        private const double IdleInterval = 2.0;
        private const int WindowRounds = 3;
        private const int ItemsPerStep = 16;
        private const int FluidPerStepUL = 1_000_000;
        private static readonly double FrameBudgetTicks = Stopwatch.Frequency * 0.002;

        internal sealed class Binding : IDisposable
        {
            internal readonly Job Owner;
            internal readonly Func<bool> CanRun;
            internal readonly Func<int, int> Transfer;
            internal TransferDirection Direction => Owner.Key.Direction;

            internal Binding(Job owner, Func<bool> canRun, Func<int, int> transfer)
            {
                Owner = owner;
                CanRun = canRun;
                Transfer = transfer;
            }

            internal void RunManually()
            {
                int limit = Owner.AttemptLimit;
                if (limit > 0 && Owner.Enabled() && Owner.Bindings.Contains(this) && CanRun())
                    Transfer(limit);
            }

            public void Dispose()
            {
                if (!Owner.Bindings.Remove(this) || Owner.Bindings.Count != 0) return;
                due.Remove(Owner);
                jobs.Remove(Owner.Key);
            }
        }

        internal sealed class Job
        {
            internal (object Target, TransferDirection Direction) Key;
            internal int Sequence;
            internal bool Fluid;
            internal Func<bool> Enabled;
            internal readonly List<Binding> Bindings = new();
            internal double DueAt;
            internal double Interval = ActiveInterval;

            internal int AttemptLimit => Fluid ? DigitalLogistics.Instance.Settings.MaxContinuousFluidVolumeUL.Value
                : DigitalLogistics.Instance.Settings.MaxContinuousItemTransfers.Value;
            internal int StepLimit => Math.Min(AttemptLimit, Fluid ? FluidPerStepUL : ItemsPerStep);

            internal int Run(int limit)
            {
                if (!Enabled() || limit <= 0) return 0;
                foreach (var binding in Bindings)
                    if (binding.CanRun()) return binding.Transfer(limit);
                return 0;
            }
        }

        private static readonly Dictionary<(object, TransferDirection), Job> jobs = new();
        private static readonly Dictionary<(object, TransferDirection), int> nightSpent = new();
        private static readonly SortedSet<Job> due = new(Comparer<Job>.Create((a, b) =>
        {
            int order = a.DueAt.CompareTo(b.DueAt);
            return order != 0 ? order : a.Sequence.CompareTo(b.Sequence);
        }));
        private static Coroutine coroutine;
        private static bool night;
        private static bool dispatching;
        private static int sequence;
        private static double Now => Time.timeAsDouble;

        internal static bool Available => DigitalLogistics.Instance.Enabled && !LogisticsSession.Loading
            && !SaveManager.IsDecoding && !(MapUIManager.Instance != null && MapUIManager.Instance.IsOutside);

        internal static Binding Register(object target, TransferDirection direction, bool fluid, Func<bool> enabled,
            Func<bool> canRun, Func<int, int> transfer)
        {
            var key = (target, direction);
            if (!jobs.TryGetValue(key, out var job))
            {
                job = new Job { Key = key, Sequence = sequence++, Fluid = fluid, Enabled = enabled };
                jobs.Add(key, job);
            }
            var binding = new Binding(job, canRun, transfer);
            job.Bindings.Add(binding);
            ResetDeadline(job);
            return binding;
        }

        internal static void Refresh(object target)
        {
            Refresh(TransferDirection.Output);
            Refresh(TransferDirection.Input);
            Refresh(TransferDirection.Local);

            void Refresh(TransferDirection direction)
            {
                if (jobs.TryGetValue((target, direction), out var job)) ResetDeadline(job);
            }
        }

        private static void ResetDeadline(Job job)
        {
            due.Remove(job);
            job.Interval = ActiveInterval;
            if (!job.Enabled()) return;
            job.DueAt = Now + (dispatching ? ActiveInterval : 0);
            due.Add(job);
            coroutine ??= ModHelper.StartCoroutine(DigitalLogistics.Instance.Manifest.ID, Run());
        }

        private static IEnumerator Run()
        {
            while (true)
            {
                yield return null;
                if (!night && due.Min is { } job && job.DueAt <= Now && Available) RunDue();
            }
        }

        private static void RunDue()
        {
            double now = Now;
            long start = Stopwatch.GetTimestamp();
            var trace = PerformanceTrace.Active;
            dispatching = true;
            try
            {
                while (due.Min is { } job && job.DueAt <= now)
                {
                    due.Remove(job);
                    int moved = job.Run(job.StepLimit);
                    if (job.Bindings.Count != 0 && job.Enabled() && !due.Contains(job))
                    {
                        job.Interval = moved > 0 ? ActiveInterval : Math.Min(job.Interval * 2, IdleInterval);
                        job.DueAt = Now + job.Interval;
                        due.Add(job);
                    }
                    if (Stopwatch.GetTimestamp() - start >= FrameBudgetTicks) break;
                }
            }
            finally
            {
                dispatching = false;
                trace?.RecordBatch(BusRunSource.Automatic, Stopwatch.GetTimestamp() - start);
            }
        }

        internal static void BeginNight()
        {
            night = true;
            nightSpent.Clear();
        }

        internal static void RunNightWindow()
        {
            if (!night || !Available) return;
            var trace = PerformanceTrace.Active;
            var timer = new TraceTimer(trace != null);
            var batch = jobs.Values.Where(job => job.Enabled()).OrderBy(job => job.Key.Direction)
                .ThenBy(job => job.Sequence).ToArray();
            for (int round = 0; round < WindowRounds; round++)
            {
                bool movedAny = false;
                foreach (var job in batch)
                {
                    nightSpent.TryGetValue(job.Key, out int spent);
                    int moved = job.Run(Math.Min(job.StepLimit, job.AttemptLimit - spent));
                    if (moved == 0) continue;
                    nightSpent[job.Key] = spent + moved;
                    movedAny = true;
                }
                if (!movedAny) break;
            }
            trace?.RecordBatch(BusRunSource.Nightly, timer.ElapsedTicks);
        }

        internal static void EndNight()
        {
            if (!night) return;
            night = false;
            nightSpent.Clear();
            var active = due.ToArray();
            due.Clear();
            double now = Now;
            foreach (var job in active)
            {
                job.DueAt = now + job.Interval;
                due.Add(job);
            }
        }

        internal static void Clear()
        {
            if (coroutine != null) ModHelper.StopCoroutine(DigitalLogistics.Instance.Manifest.ID, coroutine);
            coroutine = null;
            due.Clear();
            jobs.Clear();
            nightSpent.Clear();
            night = false;
            sequence = 0;
        }
    }
}
