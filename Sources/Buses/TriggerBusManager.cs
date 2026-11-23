using DigitalLogistics.Item;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DigitalLogistics.Buses
{
    internal static class TriggerBusManager
    {
        internal sealed class Connection
        {
            internal TriggerBusConfiguration Configuration;
            internal GameItem Bus;
            internal GameItem Machine;
            internal TriggerBusAction Action;
            internal IDisposable InputUpdates;
            internal TriggerBusAction.Option[] Options => Action.GetOptions?.Invoke()
                ?? Array.Empty<TriggerBusAction.Option>();
            internal string Name => Machine.GetDisplayName(preferCustomName: true);
        }

        private static readonly Dictionary<GameItem, List<Connection>> active = new();
        private static readonly HashSet<Connection> pending = new();
        private static readonly HashSet<TriggerBusConfiguration> executing = new();
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
                var configuration = BusConfigurationStore.GetTarget<TriggerBusConfiguration>(
                    $"machine:{machine.GetUniqueID()}", BusActions.Trigger);
                configuration.OptionId ??= action.GetOptions?.Invoke().First().Id;
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
            if (!DigitalLogistics.Instance.Enabled || SaveManager.IsDecoding || !configuration.OnInputUpdate
                || executing.Contains(configuration) || !CanTrigger(connection)) return;
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
                .Select(connection => (connection.Bus, CreateOperation(connection, automatic: true))));
        }

        internal static IEnumerable<BusOperation> GetOperations(GameItem bus, bool nightly)
        {
            foreach (var connection in GetConfigurations(bus))
            {
                if (connection.Configuration.Disabled) continue;
                if (!nightly) yield return CreateOperation(connection);
                else
                    foreach (var entry in BusPlanStore.GetPlans(connection.Configuration))
                        if (entry.Enabled) yield return CreateOperation(connection, entry);
            }
        }

        private static BusOperation CreateOperation(Connection connection, TriggerBusScheduleEntry entry = null,
            bool automatic = false) =>
            new(connection.Configuration, entry, BusOperationKind.Trigger,
                entry?.Limit == true ? entry.Count : DigitalLogistics.Instance.Settings.MaxContinuousTriggers.Value,
                () => (!automatic || connection.Configuration.OnInputUpdate) && CanTrigger(connection),
                (ref int remaining) => Trigger(connection, remaining));

        private static bool CanTrigger(Connection connection) => !connection.Configuration.Disabled
            && GetConfigurations(connection.Bus).Contains(connection) && !ItemAccess.IsBlocked(connection.Machine)
            && connection.Machine.contentWindow != null;

        private static void Trigger(Connection connection, int limit)
        {
            var configuration = connection.Configuration;
            if (!executing.Add(configuration)) return;
            pending.RemoveWhere(candidate => ReferenceEquals(candidate.Configuration, configuration));
            try
            {
                for (int i = 0; i < limit; i++)
                {
                    if (!CanTrigger(connection) || !connection.Action.CanTrigger(connection.Machine, configuration.OptionId))
                        break;
                    connection.Action.Trigger(connection.Machine, configuration.OptionId);
                }
            }
            finally
            {
                executing.Remove(configuration);
                BusController.RefreshConnections(connection.Bus);
            }
        }

        internal static void Clear()
        {
            if (scheduled != null) ModHelper.StopCoroutine(DigitalLogistics.Instance.Manifest.ID, scheduled);
            scheduled = null;
            foreach (var bus in active.Keys.ToArray()) Disconnect(bus);
            pending.Clear();
            executing.Clear();
            active.Clear();
        }
    }
}
