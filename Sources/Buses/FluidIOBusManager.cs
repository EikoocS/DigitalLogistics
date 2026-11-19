using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Filtering;
using DigitalLogistics.Item;
using DigitalLogistics.Storage;
using Newtonsoft.Json;
using UnityEngine;

namespace DigitalLogistics.Buses
{
    internal static class FluidIOBusManager
    {
        private const string DataKey = "fluid_io_bus_configuration";

        [Serializable]
        internal sealed class Configuration
        {
            public string Key;
            public string Name = string.Empty;
            public bool Disabled = true;
            public string Filter = string.Empty;
            public FluidVolume Amount = new();
            public bool UnlimitedTransfer;
            public List<FluidBusScheduleEntry> InputSchedule = new();
            public List<FluidBusScheduleEntry> OutputSchedule = new();
        }

        internal sealed class Connection
        {
            internal Configuration Configuration;
            internal GameItem Bus;
            internal FluidEndpoint Endpoint;
            internal CachedItemExpression Expression;
            internal Action OnAdded;
            internal string DefaultName => Endpoint.Name;
            internal string DefaultFilter => Bus.identifier == FluidIOBus.InputId ? "True" : "False";
        }

        private static readonly Dictionary<string, Configuration> configurations = new();
        private static readonly Dictionary<GameItem, List<Connection>> active = new();
        private static readonly HashSet<GameItem> pending = new();
        private static Coroutine scheduled;

        internal static IEnumerable<Connection> GetConfigurations(GameItem bus) =>
            active.TryGetValue(bus, out var connections) ? connections : Enumerable.Empty<Connection>();

        internal static void ApplyConfiguration(Connection connection) => connection.Expression =
            CachedItemExpressionParser.Parse(string.IsNullOrWhiteSpace(connection.Configuration.Filter)
                ? connection.DefaultFilter : connection.Configuration.Filter);

        internal static void Acquire(GameItem bus, FluidEndpoint endpoint)
        {
            string key = bus.GetUniqueID() + ":" + endpoint.Key;
            if (!configurations.TryGetValue(key, out var configuration))
                configurations.Add(key, configuration = new Configuration { Key = key });
            var connection = new Connection { Configuration = configuration, Bus = bus, Endpoint = endpoint };
            ApplyConfiguration(connection);
            connection.OnAdded = () =>
            {
                if (!configuration.Disabled && !SaveManager.IsDecoding && IOBusManager.IsOnAdd(bus)) Schedule(bus);
            };
            endpoint.Added += connection.OnAdded;
            if (!active.TryGetValue(bus, out var connections)) active.Add(bus, connections = new());
            connections.Add(connection);
        }

        internal static void Release(GameItem bus, FluidEndpoint endpoint)
        {
            var connections = active[bus];
            var connection = connections.Single(row => row.Endpoint == endpoint);
            endpoint.Added -= connection.OnAdded;
            connections.Remove(connection);
            if (connections.Count != 0) return;
            active.Remove(bus);
            pending.Remove(bus);
        }

        internal static void Load()
        {
            string json = ModHelper.GetModData(DigitalLogistics.Instance.Manifest.ID, DataKey);
            if (string.IsNullOrEmpty(json)) return;
            foreach (var configuration in JsonConvert.DeserializeObject<List<Configuration>>(json))
                configurations.Add(configuration.Key, configuration);
        }
        internal static void Save() => ModHelper.SetModData(DigitalLogistics.Instance.Manifest.ID, DataKey,
            JsonConvert.SerializeObject(active.Values.SelectMany(rows => rows).Select(row => row.Configuration).ToList()));

        internal static void Clear()
        {
            if (scheduled != null) ModHelper.StopCoroutine(DigitalLogistics.Instance.Manifest.ID, scheduled);
            scheduled = null;
            pending.Clear();
            foreach (var bus in active.Keys.ToArray())
                foreach (var connection in active[bus].ToArray()) Release(bus, connection.Endpoint);
            configurations.Clear();
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
            BusController.Run(buses);
        }

        internal static IEnumerable<BusOperation> GetOperations(GameItem bus, bool nightly)
        {
            foreach (var connection in GetConfigurations(bus))
            {
                if (connection.Configuration.Disabled) continue;
                if (FluidIOBus.CanInput(bus))
                    foreach (var operation in GetOperations(connection, true, nightly)) yield return operation;
                if (FluidIOBus.CanOutput(bus))
                    foreach (var operation in GetOperations(connection, false, nightly)) yield return operation;
            }
        }
        private static IEnumerable<BusOperation> GetOperations(Connection connection, bool input, bool nightly)
        {
            if (!nightly)
            {
                yield return CreateOperation(connection, input);
                yield break;
            }
            foreach (var entry in input ? connection.Configuration.InputSchedule : connection.Configuration.OutputSchedule)
                if (entry.Enabled) yield return CreateOperation(connection, input, entry);
        }
        private static BusOperation CreateOperation(Connection connection, bool input, FluidBusScheduleEntry entry = null) =>
            new(entry?.Order ?? 6, entry?.Priority ?? 0, input ? BusOperationKind.Input : BusOperationKind.Output,
                () => Transfer(connection, input, entry));

        private static void Transfer(Connection connection, bool input, FluidBusScheduleEntry entry)
        {
            if (connection.Configuration.Disabled || !GetConfigurations(connection.Bus).Contains(connection)
                || !connection.Endpoint.Available) return;
            connection.Endpoint.Refresh();
            var local = connection.Endpoint.Providers.ToArray();
            var network = DigitalLogistics.Manager.FluidProviders.Except(local)
                .OrderByDescending(provider => provider.Priority).ToArray();
            var expression = entry?.Expression ?? (input && connection.Bus.identifier == FluidIOBus.InputOutputId
                ? connection.Expression.Not() : connection.Expression);
            bool unlimited = entry?.UnlimitedTransfer ?? connection.Configuration.UnlimitedTransfer;
            int remaining = unlimited ? DigitalLogistics.Instance.Settings.MaxContinuousFluidVolumeUL.Value
                : (entry?.Amount ?? connection.Configuration.Amount).VolumeUL;
            var sources = input ? local : network;
            var targets = input ? network : local;
            foreach (var source in sources)
                foreach (var target in targets)
                {
                    if (remaining <= 0) return;
                    if (!source.Available || !expression.Evaluate(source.Content)
                        || !input && !source.Filter.Evaluate(source.Content)) continue;
                    remaining -= FluidTransfer.Transfer(source, target, remaining, applyTargetFilter: input).RemovedUL;
                }
        }
    }
}
