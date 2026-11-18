using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Item;
using DigitalLogistics.Controller;
using UnityEngine;

namespace DigitalLogistics.Storage
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
            [NonSerialized] internal GameItem Bus;
            [NonSerialized] internal GameItem Machine;
            [NonSerialized] internal TriggerBusAction Action;
            [NonSerialized] internal IDisposable InputUpdates;
            internal TriggerBusAction.Option[] Options => Action.GetOptions?.Invoke()
                ?? Array.Empty<TriggerBusAction.Option>();
            internal string Name => Machine.GetDisplayName(preferCustomName: true);
            internal int TriggerCount => LoopTrigger ? DigitalLogistics.Instance.Settings.MaxContinuousTriggers.Value : Count;
        }

        [Serializable]
        private sealed class SaveData
        {
            public List<Configuration> Configurations = new();
        }

        private static readonly Dictionary<string, Configuration> configurations = new();
        private static readonly Dictionary<GameItem, List<Configuration>> active = new();
        private static readonly HashSet<Configuration> pending = new();
        private static readonly HashSet<Configuration> executing = new();
        private static Coroutine scheduled;

        internal static IEnumerable<Configuration> GetConfigurations(GameItem bus) =>
            active.TryGetValue(bus, out var rows) ? rows : Enumerable.Empty<Configuration>();

        internal static void Connect(GameItem bus, IEnumerable<GameItem> targets)
        {
            var rows = new List<Configuration>();
            foreach (var machine in targets.Where(item => item != null && item.contentWindow != null).Distinct())
            {
                if (!TriggerBusActions.Actions.TryGetValue(machine.identifier, out var action)) continue;
                string key = bus.GetUniqueID() + ":" + machine.GetUniqueID();
                if (!configurations.TryGetValue(key, out var configuration))
                {
                    configuration = new Configuration
                    {
                        Key = key,
                        OptionId = action.GetOptions?.Invoke().First().Id
                    };
                    configurations.Add(key, configuration);
                }
                configuration.Bus = bus;
                configuration.Machine = machine;
                configuration.Action = action;
                rows.Add(configuration);
            }
            foreach (var removed in GetConfigurations(bus).Except(rows).ToArray()) ReleaseInputs(removed);
            active[bus] = rows;
            foreach (var configuration in rows)
                configuration.InputUpdates ??= configuration.Action.SubscribeInputs?.Invoke(
                    configuration.Machine, () => Schedule(configuration));
        }

        private static void ReleaseInputs(Configuration configuration)
        {
            configuration.InputUpdates?.Dispose();
            configuration.InputUpdates = null;
            pending.Remove(configuration);
        }

        internal static void Disconnect(GameItem bus)
        {
            foreach (var configuration in GetConfigurations(bus)) ReleaseInputs(configuration);
            active.Remove(bus);
        }

        private static void Schedule(Configuration configuration)
        {
            if (!DigitalLogistics.Instance.Enabled || SaveManager.IsDecoding || configuration.Disabled
                || !configuration.OnInputUpdate || executing.Contains(configuration)) return;
            pending.Add(configuration);
            if (scheduled == null)
                scheduled = ModHelper.StartCoroutine(DigitalLogistics.Instance.Manifest.ID, RunPending());
        }

        private static IEnumerator RunPending()
        {
            yield return null;
            scheduled = null;
            var configurations = pending.ToArray();
            pending.Clear();
            BusController.Run(configurations.Where(configuration => configuration.OnInputUpdate)
                .Select(configuration => (configuration.Bus, CreateOperation(configuration))));
        }

        internal static IEnumerable<BusOperation> GetOperations(GameItem bus, bool nightly)
        {
            foreach (var configuration in GetConfigurations(bus))
            {
                if (configuration.Disabled) continue;
                if (!nightly) yield return CreateOperation(configuration);
                else
                    foreach (var entry in configuration.Schedule)
                        if (entry.Enabled) yield return CreateOperation(configuration, entry);
            }
        }

        private static BusOperation CreateOperation(Configuration configuration, BusScheduleEntry entry = null) =>
            new(entry?.Order ?? 6, entry?.Priority ?? 0, BusOperationKind.Trigger,
                () => Trigger(configuration, entry?.RepeatCount ?? configuration.TriggerCount));

        private static void Trigger(Configuration configuration, int limit)
        {
            if (!executing.Add(configuration)) return;
            pending.Remove(configuration);
            try
            {
                for (int i = 0; i < limit; i++)
                {
                    if (configuration.Disabled || !GetConfigurations(configuration.Bus).Contains(configuration)
                        || !GeneralHelper.IsItemOwned(configuration.Machine)
                        || configuration.Machine.contentWindow == null
                        || !configuration.Action.CanTrigger(configuration.Machine, configuration.OptionId)) break;
                    configuration.Action.Trigger(configuration.Machine, configuration.OptionId);
                }
            }
            finally
            {
                executing.Remove(configuration);
                BusController.RefreshConnections(configuration.Bus);
            }
        }

        internal static void Load()
        {
            string json = ModHelper.GetModData(DigitalLogistics.Instance.Manifest.ID, DataKey);
            if (string.IsNullOrEmpty(json)) return;
            foreach (var configuration in JsonUtility.FromJson<SaveData>(json).Configurations)
                configurations.Add(configuration.Key, configuration);
        }

        internal static void Save() => ModHelper.SetModData(DigitalLogistics.Instance.Manifest.ID, DataKey,
            JsonUtility.ToJson(new SaveData { Configurations = configurations.Values.ToList() }));

        internal static void Clear()
        {
            if (scheduled != null) ModHelper.StopCoroutine(DigitalLogistics.Instance.Manifest.ID, scheduled);
            scheduled = null;
            foreach (var bus in active.Keys.ToArray()) Disconnect(bus);
            pending.Clear();
            active.Clear();
            configurations.Clear();
        }
    }
}
