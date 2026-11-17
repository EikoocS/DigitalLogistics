using System.Collections.Generic;
using System.Linq;
using DigitalStorage.Item;
using DigitalStorage.Storage;

namespace DigitalStorage.Controller
{
    internal static class BusController
    {
        private static readonly Dictionary<GameItem, Bus> connections = new();
        private static bool running;
        private static BusOperationKind currentKind;
        private static bool Available => DigitalStorage.Instance.Enabled
            && !(MapUIManager.Instance != null && MapUIManager.Instance.IsOutside);
        internal static bool CanRun => Available && !running;
        internal static bool CanSchedule => Available && (!running || currentKind == BusOperationKind.Trigger);

        internal static void Attach(Bus bus) => connections.Add(bus.Item, bus);
        internal static void Detach(Bus bus) => connections.Remove(bus.Item);

        internal static void ClearConnections()
        {
            StorageIndexRefresh.Clear();
            foreach (var bus in connections.Values.ToArray()) bus.Disconnect();
            IOBusManager.Clear();
            TriggerBusManager.Clear();
            BusManager.Clear();
        }

        internal static void RefreshConnections(GameItem item)
        {
            if (connections.TryGetValue(item, out var bus)) bus.Refresh();
        }

        internal static IEnumerable<object> GetConfigurations(GameItem item) =>
            connections.TryGetValue(item, out var bus) ? bus.Configurations : Enumerable.Empty<object>();

        internal static void Run(IEnumerable<GameItem> items) => Execute(GetOperations(
            items.Where(connections.ContainsKey).Select(item => connections[item])));

        internal static void Run(IEnumerable<(GameItem Item, BusOperation Operation)> operations) => Execute(operations
            .Where(entry => connections.ContainsKey(entry.Item))
            .Select(entry => (connections[entry.Item], entry.Operation)));

        internal static void RunWindow(int window) => Execute(GetOperations(connections.Values, nightly: true)
            .Where(entry => HopperHelper.WindowOf(entry.Operation.Order) == window));

        private static IEnumerable<(Bus Bus, BusOperation Operation)> GetOperations(IEnumerable<Bus> buses,
            bool nightly = false)
        {
            foreach (var bus in buses)
                if (bus is ITriggerableBus triggerable)
                    foreach (var operation in triggerable.GetOperations(nightly))
                        yield return (bus, operation);
        }

        private static void Execute(IEnumerable<(Bus Bus, BusOperation Operation)> operations)
        {
            if (!CanRun) return;
            var ordered = operations.OrderBy(entry => entry.Operation.Order)
                .ThenByDescending(entry => entry.Operation.Priority)
                .ThenBy(entry => entry.Operation.Kind).ToArray();
            running = true;
            try
            {
                foreach (var entry in ordered)
                {
                    if (!entry.Bus.CanExecute) continue;
                    currentKind = entry.Operation.Kind;
                    entry.Operation.Execute();
                }
            }
            finally { running = false; }
        }
    }
}
