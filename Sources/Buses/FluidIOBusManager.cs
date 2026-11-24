using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Item;
using DigitalLogistics.Storage;
using DigitalLogistics.Tracing;
using UnityEngine;

namespace DigitalLogistics.Buses
{
    internal static class FluidIOBusManager
    {
        internal sealed class Connection
        {
            internal FluidTransferBusConfiguration Configuration;
            internal GameItem Bus;
            internal FluidEndpoint Endpoint;
            internal Action OnChanged;
            internal string DefaultName => Endpoint.Name;
            internal string DefaultFilter => Bus.identifier == FluidIOBus.InputId ? "True" : "False";
        }

        private static readonly Dictionary<GameItem, List<Connection>> active = new();
        private static readonly HashSet<GameItem> pending = new();
        private static Coroutine scheduled;

        internal static IEnumerable<Connection> GetConfigurations(GameItem bus) =>
            active.TryGetValue(bus, out var connections) ? connections : Enumerable.Empty<Connection>();

        internal static void Acquire(GameItem bus, FluidEndpoint endpoint)
        {
            var configuration = BusConfigurationStore.GetTarget<FluidTransferBusConfiguration>(
                endpoint.Key, BusActions.ForTransfer(bus));
            var connection = new Connection { Configuration = configuration, Bus = bus, Endpoint = endpoint };
            connection.OnChanged = () =>
            {
                if (!configuration.Disabled && !SaveManager.IsDecoding && IOBusManager.IsOnChanged(bus)) Schedule(bus);
            };
            endpoint.Changed += connection.OnChanged;
            if (!active.TryGetValue(bus, out var connections)) active.Add(bus, connections = new());
            connections.Add(connection);
        }

        internal static void Release(GameItem bus, FluidEndpoint endpoint)
        {
            var connections = active[bus];
            var connection = connections.Single(row => row.Endpoint == endpoint);
            endpoint.Changed -= connection.OnChanged;
            connections.Remove(connection);
            if (connections.Count != 0) return;
            active.Remove(bus);
            pending.Remove(bus);
        }

        internal static void Clear()
        {
            if (scheduled != null) ModHelper.StopCoroutine(DigitalLogistics.Instance.Manifest.ID, scheduled);
            scheduled = null;
            pending.Clear();
            foreach (var bus in active.Keys.ToArray())
                foreach (var connection in active[bus].ToArray()) Release(bus, connection.Endpoint);
        }

        internal static void Schedule(GameItem bus)
        {
            if (!BusController.CanSchedule || !active.ContainsKey(bus)) return;
            pending.Add(bus);
            if (scheduled == null)
                scheduled = ModHelper.StartCoroutine(DigitalLogistics.Instance.Manifest.ID, RunPending());
        }
        private static IEnumerator RunPending()
        {
            yield return null;
            scheduled = null;
            var buses = pending.ToArray();
            pending.Clear();
            BusController.Run(buses, BusRunSource.Automatic);
        }

        internal static IEnumerable<BusOperation> GetOperations(GameItem bus, bool nightly)
        {
            foreach (var connection in GetConfigurations(bus))
            {
                var configuration = connection.Configuration;
                if (configuration.Disabled) continue;
                foreach (var operation in BusOperation.ForTransfers(configuration, FluidIOBus.CanInput(bus), FluidIOBus.CanOutput(bus),
                    nightly ? BusPlanStore.GetPlans<FluidBusScheduleEntry>(configuration) : null,
                    DigitalLogistics.Instance.Settings.MaxContinuousFluidVolumeUL.Value, entry => entry.Amount.VolumeUL,
                    () => !configuration.Disabled && GetConfigurations(bus).Contains(connection) && connection.Endpoint.Available,
                    (bool input, FluidBusScheduleEntry entry, ref int remaining) => Transfer(connection, input, entry, ref remaining)))
                    yield return operation;
            }
        }
        private static void Transfer(Connection connection, bool input, FluidBusScheduleEntry entry, ref int remaining)
        {
            connection.Endpoint.Refresh();
            var local = connection.Endpoint.Providers.ToArray();
            var network = DigitalLogistics.Manager.FluidProviders.Except(local)
                .OrderByDescending(provider => provider.Priority).ToArray();
            var expression = entry?.Expression ?? connection.Configuration.GetExpression(connection.DefaultFilter);
            if (input && connection.Bus.identifier == FluidIOBus.InputOutputId) expression = expression.Not();
            bool avoidMixing = !input && connection.Configuration.AvoidMixing;
            var sources = input ? local : network;
            var targets = input ? network : local;
            foreach (var source in sources)
                foreach (var target in targets)
                {
                    if (remaining <= 0) return;
                    if (avoidMixing && target.VolumeUL > 0) continue;
                    if (!source.Available || !expression.Evaluate(source.Content)
                        || !input && !source.Filter.Evaluate(source.Content)) continue;
                    remaining -= FluidTransfer.Transfer(source, target, remaining, applyTargetFilter: input).RemovedUL;
                }
        }
    }
}
