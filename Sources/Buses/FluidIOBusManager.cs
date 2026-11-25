using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Item;
using DigitalLogistics.Storage;

namespace DigitalLogistics.Buses
{
    internal static class FluidIOBusManager
    {
        internal sealed class Connection
        {
            internal FluidTransferBusConfiguration Configuration;
            internal GameItem Bus;
            internal FluidEndpoint Endpoint;
            internal LogisticsScheduler.Binding[] Polling;
            internal string DefaultName => Endpoint.Name;
            internal string DefaultFilter => Bus.identifier == FluidIOBus.InputId ? "True" : "False";
        }

        private static readonly Dictionary<GameItem, List<Connection>> active = new();
        internal static IEnumerable<Connection> GetConfigurations(GameItem bus) =>
            active.TryGetValue(bus, out var connections) ? connections : Enumerable.Empty<Connection>();

        internal static void Acquire(GameItem bus, FluidEndpoint endpoint)
        {
            var configuration = BusConfigurationStore.GetTarget<FluidTransferBusConfiguration>(
                endpoint.Key, BusActions.ForTransfer(bus));
            var connection = new Connection { Configuration = configuration, Bus = bus, Endpoint = endpoint };
            if (!active.TryGetValue(bus, out var connections)) active.Add(bus, connections = new());
            connections.Add(connection);
            connection.Polling = BusController.BindTransfers(bus, configuration, FluidIOBus.CanInput(bus),
                FluidIOBus.CanOutput(bus), fluid: true, () => endpoint.Available,
                (input, limit) => Transfer(connection, input, limit));
        }

        internal static void Release(GameItem bus, FluidEndpoint endpoint)
        {
            var connections = active[bus];
            var connection = connections.Single(row => row.Endpoint == endpoint);
            foreach (var binding in connection.Polling) binding.Dispose();
            connections.Remove(connection);
            if (connections.Count != 0) return;
            active.Remove(bus);
        }

        internal static void Clear()
        {
            foreach (var bus in active.Keys.ToArray())
                foreach (var connection in active[bus].ToArray()) Release(bus, connection.Endpoint);
        }

        private static int Transfer(Connection connection, bool input, int limit)
        {
            int remaining = limit;
            connection.Endpoint.Refresh();
            var local = connection.Endpoint.Providers.ToArray();
            var network = DigitalLogistics.Manager.FluidProviders.Except(local)
                .OrderByDescending(provider => provider.Priority).ToArray();
            var expression = connection.Configuration.GetExpression(connection.DefaultFilter);
            if (input && connection.Bus.identifier == FluidIOBus.InputOutputId) expression = expression.Not();
            bool avoidMixing = !input && connection.Configuration.AvoidMixing;
            var sources = input ? local : network;
            var targets = input ? network : local;
            foreach (var source in sources)
            {
                foreach (var target in targets)
                {
                    if (avoidMixing && target.VolumeUL > 0) continue;
                    if (!source.Available || !expression.Evaluate(source.Content)
                        || !input && !source.Filter.Evaluate(source.Content)) continue;
                    remaining -= FluidTransfer.Transfer(source, target, remaining, applyTargetFilter: input).RemovedUL;
                    if (remaining <= 0) return limit;
                }
            }
            return limit - remaining;
        }
    }
}
