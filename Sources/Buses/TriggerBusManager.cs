using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace DigitalLogistics.Buses
{
    internal static class TriggerBusManager
    {
        private const string DataKey = "trigger_bus_configuration";

        [Serializable]
        internal sealed class Configuration
        {
            public string Key;
            public bool Disabled = true;
            public List<BusScheduleEntry> Schedule = new();
            public int Count = 1;
            public bool LoopTrigger;
            public bool OnInputUpdate;
            public string OptionId;
        }

        internal sealed class Connection
        {
            internal Configuration Configuration;
            internal GameItem Bus;
            internal GameItem Machine;
            internal TriggerBusAction Action;
            internal IDisposable InputUpdates;
            internal TriggerBusAction.Option[] Options => Action.GetOptions?.Invoke()
                ?? Array.Empty<TriggerBusAction.Option>();
            internal string Name => Machine.GetDisplayName(preferCustomName: true);
            internal int TriggerCount => Configuration.LoopTrigger
                ? DigitalLogistics.Instance.Settings.MaxContinuousTriggers.Value : Configuration.Count;
        }

        [Serializable]
        private sealed class SaveData
        {
            public List<Configuration> Configurations = new();
        }

        private static readonly Dictionary<string, Configuration> configurations = new();
        private static readonly Dictionary<GameItem, List<Connection>> active = new();
        private static readonly HashSet<Connection> pending = new();
        private static readonly HashSet<Connection> executing = new();
        private static Coroutine scheduled;

        internal static IEnumerable<Connection> GetConfigurations(GameItem bus) =>
            active.TryGetValue(bus, out var rows) ? rows : Enumerable.Empty<Connection>();

        internal static void Connect(GameItem bus, IEnumerable<GameItem> targets)
        {
            var previous = GetConfigurations(bus).ToArray();
            var rows = new List<Connection>();
            foreach (var machine in targets.Where(item => item != null && item.contentWindow != null).Distinct())
            {
                if (!TriggerBusActions.Actions.TryGetValue(machine.identifier, out var action)) continue;
                string key = bus.GetUniqueID() + ":" + machine.GetUniqueID();
                if (!configurations.TryGetValue(key, out var configuration))
                {
                    configuration = new Configuration { Key = key, OptionId = action.GetOptions?.Invoke().First().Id };
                    configurations.Add(key, configuration);
                }
                var connection = previous.FirstOrDefault(row => row.Configuration == configuration)
                    ?? new Connection { Configuration = configuration, Bus = bus, Machine = machine, Action = action };
                rows.Add(connection);
            }
            foreach (var removed in previous.Except(rows)) ReleaseInputs(removed);
            active[bus] = rows;
            foreach (var connection in rows)
                connection.InputUpdates ??= connection.Action.SubscribeInputs?.Invoke(
                    connection.Machine, () => Schedule(connection));
        }

        private static void ReleaseInputs(Connection connection)
        {
            connection.InputUpdates?.Dispose();
            connection.InputUpdates = null;
            pending.Remove(connection);
        }

        internal static void Disconnect(GameItem bus)
        {
            foreach (var connection in GetConfigurations(bus)) ReleaseInputs(connection);
            active.Remove(bus);
        }

        private static void Schedule(Connection connection)
        {
            var configuration = connection.Configuration;
            if (!DigitalLogistics.Instance.Enabled || SaveManager.IsDecoding || configuration.Disabled
                || !configuration.OnInputUpdate || executing.Contains(connection)
                || BusAccess.IsBlocked(connection.Machine)) return;
            pending.Add(connection);
            if (scheduled == null)
                scheduled = ModHelper.StartCoroutine(DigitalLogistics.Instance.Manifest.ID, RunPending());
        }

        private static IEnumerator RunPending()
        {
            yield return null;
            scheduled = null;
            var connections = pending.ToArray();
            pending.Clear();
            BusController.Run(connections.Where(connection => connection.Configuration.OnInputUpdate)
                .Select(connection => (connection.Bus, CreateOperation(connection))));
        }

        internal static IEnumerable<BusOperation> GetOperations(GameItem bus, bool nightly)
        {
            foreach (var connection in GetConfigurations(bus))
            {
                if (connection.Configuration.Disabled) continue;
                if (!nightly) yield return CreateOperation(connection);
                else
                    foreach (var entry in connection.Configuration.Schedule)
                        if (entry.Enabled) yield return CreateOperation(connection, entry);
            }
        }

        private static BusOperation CreateOperation(Connection connection, BusScheduleEntry entry = null) =>
            new(entry?.Order ?? 6, entry?.Priority ?? 0, BusOperationKind.Trigger,
                () => Trigger(connection, entry?.RepeatCount ?? connection.TriggerCount));

        private static void Trigger(Connection connection, int limit)
        {
            if (!executing.Add(connection)) return;
            pending.Remove(connection);
            try
            {
                for (int i = 0; i < limit; i++)
                {
                    if (connection.Configuration.Disabled || !GetConfigurations(connection.Bus).Contains(connection)
                        || BusAccess.IsBlocked(connection.Machine)
                        || connection.Machine.contentWindow == null
                        || !connection.Action.CanTrigger(connection.Machine, connection.Configuration.OptionId)) break;
                    connection.Action.Trigger(connection.Machine, connection.Configuration.OptionId);
                }
            }
            finally
            {
                executing.Remove(connection);
                BusController.RefreshConnections(connection.Bus);
            }
        }

        internal static void Load()
        {
            string json = ModHelper.GetModData(DigitalLogistics.Instance.Manifest.ID, DataKey);
            if (string.IsNullOrEmpty(json)) return;
            foreach (var configuration in JsonConvert.DeserializeObject<SaveData>(json).Configurations)
                configurations.Add(configuration.Key, configuration);
        }

        internal static void Save() => ModHelper.SetModData(DigitalLogistics.Instance.Manifest.ID, DataKey,
            JsonConvert.SerializeObject(new SaveData { Configurations = configurations.Values.ToList() }));

        internal static void Clear()
        {
            if (scheduled != null) ModHelper.StopCoroutine(DigitalLogistics.Instance.Manifest.ID, scheduled);
            scheduled = null;
            foreach (var bus in active.Keys.ToArray()) Disconnect(bus);
            pending.Clear();
            executing.Clear();
            active.Clear();
            configurations.Clear();
        }
    }
}
