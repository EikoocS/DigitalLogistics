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
        internal int Order { get; }
        internal int SubOrder { get; }
        internal BusOperationKind Kind { get; }
        internal Action Execute { get; }

        internal BusOperation(BusScheduleEntry entry, BusOperationKind kind, Action execute)
        {
            Order = entry?.Order ?? 6;
            SubOrder = entry?.SubOrder ?? 0;
            Kind = kind;
            Execute = execute;
        }

        internal delegate void TransferStep<T>(bool input, T entry, ref int remaining) where T : BusScheduleEntry;

        internal static IEnumerable<BusOperation> ForTransfers<T>(bool canInput, bool canOutput,
            IEnumerable<T> schedule, int continuousLimit, Func<T, int> getLimit, TransferStep<T> transfer)
            where T : BusScheduleEntry
        {
            if (schedule == null)
            {
                if (canOutput) yield return Create(null, false);
                if (canInput) yield return Create(null, true);
                yield break;
            }
            foreach (var entry in schedule)
            {
                if (!entry.Enabled) continue;
                if (!entry.Limit)
                {
                    if (canOutput) yield return Create(entry, false);
                    if (canInput) yield return Create(entry, true);
                    continue;
                }
                int remaining = getLimit(entry);
                if (canOutput) yield return new BusOperation(entry, BusOperationKind.Output,
                    () => transfer(false, entry, ref remaining));
                if (canInput) yield return new BusOperation(entry, BusOperationKind.Input,
                    () => transfer(true, entry, ref remaining));
            }

            BusOperation Create(T entry, bool input) => new(entry, input ? BusOperationKind.Input : BusOperationKind.Output,
                () =>
                {
                    int remaining = continuousLimit;
                    transfer(input, entry, ref remaining);
                });
        }
    }
}
