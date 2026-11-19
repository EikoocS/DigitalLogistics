using System;
using System.Collections.Generic;
using System.Linq;

namespace DigitalLogistics.Buses
{
    internal static class BusPlanStore
    {
        private static readonly Dictionary<string, List<(string Action, BusScheduleEntry Entry)>> plans = new();
        internal static int Revision { get; private set; }

        internal static IReadOnlyList<(string Action, BusScheduleEntry Entry)> GetPlans(string targetId) =>
            plans.TryGetValue(targetId, out var entries) ? entries : Array.Empty<(string, BusScheduleEntry)>();

        internal static IEnumerable<T> GetPlans<T>(ScheduledBusConfiguration<T> target) where T : BusScheduleEntry =>
            GetPlans(target.TargetId).Where(plan => plan.Action == target.Action).Select(plan => (T)plan.Entry);

        internal static void Add(BusTargetConfiguration target, BusScheduleEntry entry)
        {
            if (!plans.TryGetValue(target.TargetId, out var entries))
                plans.Add(target.TargetId, entries = new());
            entries.Add((target.Action, entry));
            Revision++;
        }

        internal static void Remove(BusTargetConfiguration target, BusScheduleEntry entry)
        {
            plans[target.TargetId].Remove((target.Action, entry));
            Revision++;
        }

        internal static void Clear()
        {
            plans.Clear();
            Revision++;
        }
    }
}
