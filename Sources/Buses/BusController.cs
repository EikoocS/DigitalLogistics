using System;
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

        internal static void Attach(Bus bus) => connections.Add(bus.Item, bus);
        internal static void Detach(Bus bus) => connections.Remove(bus.Item);

        internal static void ClearConnections()
        {
            foreach (var bus in connections.Values.ToArray()) bus.Disconnect();
        }

        internal static IEnumerable<object> GetConfigurations(GameItem bus) =>
            connections.TryGetValue(bus, out var connection) ? connection.Configurations : Enumerable.Empty<object>();

        internal static LogisticsScheduler.Binding[] BindTransfers(GameItem bus, TransferBusConfiguration configuration,
            bool canInput, bool canOutput, bool fluid, Func<bool> available, Func<bool, int, int> transfer)
        {
            var bindings = new List<LogisticsScheduler.Binding>(2);
            if (canOutput) bindings.Add(Bind(TransferDirection.Output));
            if (canInput) bindings.Add(Bind(TransferDirection.Input));
            return bindings.ToArray();

            LogisticsScheduler.Binding Bind(TransferDirection direction) =>
                LogisticsScheduler.Register(configuration, direction, fluid, () => !configuration.Disabled,
                    () => connections.TryGetValue(bus, out var connection) && connection.CanExecute && available(),
                    limit => RunTransfer(bus, direction, limit, transfer));
        }

        private static int RunTransfer(GameItem bus, TransferDirection direction, int limit, Func<bool, int, int> transfer)
        {
            var trace = PerformanceTrace.Active;
            var timer = new TraceTimer(trace != null);
            try { return transfer(direction == TransferDirection.Input, limit); }
            finally
            {
                trace?.RecordOperation(bus.identifier, direction, bus.uniqueId, timer.ElapsedTicks);
            }
        }

        internal static void RunManually(Bus bus)
        {
            if (running || !LogisticsScheduler.Available || !bus.CanExecute || bus is not ITransferBus transferBus)
                return;
            var trace = PerformanceTrace.Active;
            var timer = new TraceTimer(trace != null);
            running = true;
            try
            {
                foreach (var binding in transferBus.Transfers.OrderBy(binding => binding.Direction).ToArray())
                    binding.RunManually();
            }
            finally
            {
                running = false;
                trace?.RecordBatch(BusRunSource.Manual, timer.ElapsedTicks);
            }
        }
    }
}
