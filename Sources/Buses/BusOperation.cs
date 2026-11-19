using System;
using System.Collections.Generic;

namespace DigitalLogistics.Buses
{
    internal enum BusOperationKind
    {
        Output,
        Trigger,
        Input
    }

    internal readonly struct BusOperation
    {
        internal BusTargetConfiguration Target { get; }
        internal BusScheduleEntry Plan { get; }
        internal int Order => Plan?.Order ?? 6;
        internal int SubOrder => Plan?.SubOrder ?? 0;
        internal BusOperationKind Kind { get; }
        internal int Limit { get; }
        internal Func<bool> CanExecute { get; }
        internal Run Execute { get; }

        internal delegate void Run(ref int remaining);

        internal BusOperation(BusTargetConfiguration target, BusScheduleEntry plan, BusOperationKind kind,
            int limit, Func<bool> canExecute, Run execute)
        {
            Target = target;
            Plan = plan;
            Kind = kind;
            Limit = limit;
            CanExecute = canExecute;
            Execute = execute;
        }

        internal delegate void TransferStep<T>(bool input, T entry, ref int remaining) where T : BusScheduleEntry;

        internal static IEnumerable<BusOperation> ForTransfers<T>(BusTargetConfiguration target,
            bool canInput, bool canOutput, IEnumerable<T> schedule, int continuousLimit, Func<T, int> getLimit,
            Func<bool> canExecute, TransferStep<T> transfer)
            where T : BusScheduleEntry
        {
            if (schedule == null)
            {
                if (canOutput) yield return Create(null, false, continuousLimit);
                if (canInput) yield return Create(null, true, continuousLimit);
                yield break;
            }
            foreach (var entry in schedule)
            {
                if (!entry.Enabled) continue;
                int limit = entry.Limit ? getLimit(entry) : continuousLimit;
                if (canOutput) yield return Create(entry, false, limit);
                if (canInput) yield return Create(entry, true, limit);
            }

            BusOperation Create(T entry, bool input, int limit) =>
                new(target, entry, input ? BusOperationKind.Input : BusOperationKind.Output, limit, canExecute,
                    (ref int remaining) => transfer(input, entry, ref remaining));
        }
    }
}
