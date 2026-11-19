using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Item;
using DigitalLogistics.Tracing;

namespace DigitalLogistics.Buses
{
    internal static class BusController
    {
        private static readonly Dictionary<GameItem, Bus> connections = new();
        private static bool running;
        private static BusOperationKind currentKind;
        private static bool Available => DigitalLogistics.Instance.Enabled
            && !(MapUIManager.Instance != null && MapUIManager.Instance.IsOutside);
        internal static bool CanRun => Available && !running;
        internal static bool CanSchedule => Available && (!running || currentKind == BusOperationKind.Trigger);

        internal static void Attach(Bus bus) => connections.Add(bus.Item, bus);
        internal static void Detach(Bus bus) => connections.Remove(bus.Item);

        internal static void ClearConnections()
        {
            foreach (var bus in connections.Values.ToArray()) bus.Disconnect();
        }

        internal static void RefreshConnections(GameItem item)
        {
            if (connections.TryGetValue(item, out var bus)) bus.Refresh();
        }

        internal static IEnumerable<object> GetConfigurations(GameItem item) =>
            connections.TryGetValue(item, out var bus) ? bus.Configurations : Enumerable.Empty<object>();

        internal static void Run(IEnumerable<GameItem> items, BusRunSource source = BusRunSource.Manual) =>
            Execute(GetOperations(items.Where(connections.ContainsKey).Select(item => connections[item])), source);

        internal static void Run(IEnumerable<(GameItem Item, BusOperation Operation)> operations) => Execute(operations
            .Where(entry => connections.ContainsKey(entry.Item))
            .Select(entry => (connections[entry.Item], entry.Operation)), BusRunSource.Automatic);

        internal static void RunWindow(int window) => Execute(GetOperations(connections.Values, nightly: true)
            .Where(entry => HopperHelper.WindowOf(entry.Operation.Order) == window), BusRunSource.Nightly);

        private static IEnumerable<(Bus Bus, BusOperation Operation)> GetOperations(IEnumerable<Bus> buses,
            bool nightly = false)
        {
            foreach (var bus in buses)
                if (bus is ITriggerableBus triggerable)
                    foreach (var operation in triggerable.GetOperations(nightly))
                        yield return (bus, operation);
        }

        private static void Execute(IEnumerable<(Bus Bus, BusOperation Operation)> operations, BusRunSource source)
        {
            if (!CanRun) return;
            var trace = PerformanceTrace.Active;
            var batchTimer = new TraceTimer(trace != null);
            try
            {
                var ordered = operations.OrderBy(entry => entry.Operation.Order)
                    .ThenByDescending(entry => entry.Operation.SubOrder)
                    .ThenBy(entry => entry.Operation.Kind).ToArray();
                var executed = new HashSet<(BusTargetConfiguration, BusScheduleEntry, BusOperationKind)>();
                var planBudgets = new Dictionary<(BusTargetConfiguration, BusScheduleEntry), int>();
                running = true;
                foreach (var entry in ordered)
                {
                    var operation = entry.Operation;
                    if (!entry.Bus.CanExecute || !operation.CanExecute()) continue;
                    if (source != BusRunSource.Manual && !executed.Add((operation.Target, operation.Plan, operation.Kind)))
                        continue;
                    var planKey = (operation.Target, operation.Plan);
                    bool limited = operation.Plan?.Limit == true;
                    int remaining = limited && planBudgets.TryGetValue(planKey, out int budget)
                        ? budget : operation.Limit;
                    if (remaining <= 0) continue;
                    currentKind = operation.Kind;
                    var operationTimer = new TraceTimer(trace != null);
                    try
                    {
                        operation.Execute(ref remaining);
                        if (limited) planBudgets[planKey] = remaining;
                    }
                    finally
                    {
                        trace?.RecordOperation(entry.Bus.Item.identifier, entry.Operation.Kind,
                            entry.Bus.Item.uniqueId, operationTimer.ElapsedTicks);
                    }
                }
            }
            finally
            {
                running = false;
                trace?.RecordBatch(source, batchTimer.ElapsedTicks);
            }
        }
    }
}
