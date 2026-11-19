using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DigitalLogistics.Buses
{
    internal static class BusConfigurationStore
    {
        private const string GlobalKey = "bus.global";
        private const string TargetKey = "bus.target";
        private const string PlanKey = "bus.plan";

        private static readonly Dictionary<int, BusGlobalConfiguration> globals = new();
        private static readonly Dictionary<string, Dictionary<string, BusTargetConfiguration>> targets = new();
        private static readonly Dictionary<string, Type> recordTypes = new()
        {
            ["global"] = typeof(BusGlobalConfiguration),
            ["storage"] = typeof(StorageBusConfiguration),
            ["item_transfer"] = typeof(TransferBusConfiguration<IOBusScheduleEntry>),
            ["fluid_transfer"] = typeof(FluidTransferBusConfiguration),
            ["trigger"] = typeof(TriggerBusConfiguration),
            ["item_plan"] = typeof(IOBusScheduleEntry),
            ["fluid_plan"] = typeof(FluidBusScheduleEntry),
            ["trigger_plan"] = typeof(TriggerBusScheduleEntry)
        };
        private static readonly Dictionary<Type, string> recordNames = recordTypes.ToDictionary(pair => pair.Value, pair => pair.Key);
        private static string ModId => DigitalLogistics.Instance.Manifest.ID;

        internal static BusGlobalConfiguration FindGlobal(GameItem bus) =>
            globals.TryGetValue(bus.uniqueId, out var settings) ? settings : null;

        internal static BusGlobalConfiguration GetGlobal(GameItem bus)
        {
            int uid = bus.GetUniqueID();
            if (!globals.TryGetValue(uid, out var settings))
                globals.Add(uid, settings = new());
            return settings;
        }

        internal static T GetTarget<T>(string targetId, string action) where T : BusTargetConfiguration, new()
        {
            if (!targets.TryGetValue(targetId, out var actions))
                targets.Add(targetId, actions = new());
            if (!actions.TryGetValue(action, out var configuration))
                actions.Add(action, configuration = new T { TargetId = targetId, Action = action });
            return (T)configuration;
        }

        internal static void Load()
        {
            Clear();
            foreach (int uid in ReadIndex<int>(GlobalKey))
                if (ReadRecord<BusGlobalConfiguration>($"{GlobalKey}.{uid}", out _) is { } settings)
                    globals.Add(uid, settings);

            foreach (string targetId in ReadIndex<string>(TargetKey))
            {
                string targetKey = $"{TargetKey}.{targetId}";
                var actions = new Dictionary<string, BusTargetConfiguration>();
                foreach (string action in ReadIndex<string>(targetKey))
                    if (ReadRecord<BusTargetConfiguration>($"{targetKey}.{action}", out _) is { } configuration)
                    {
                        configuration.TargetId = targetId;
                        configuration.Action = action;
                        actions.Add(action, configuration);
                    }
                if (actions.Count == 0) continue;
                targets.Add(targetId, actions);

                string planKey = $"{PlanKey}.{targetId}";
                int count = ReadCount(planKey);
                for (int i = 0; i < count; i++)
                {
                    var plan = ReadRecord<BusScheduleEntry>($"{planKey}.{i}", out var record);
                    if (plan != null && record.Value<string>("action") is { } action
                        && actions.TryGetValue(action, out var configuration))
                        BusPlanStore.Add(configuration, plan);
                }
            }
        }

        internal static void Save()
        {
            WriteValue(GlobalKey, globals.Keys.ToArray());
            foreach (var global in globals)
                WriteRecord($"{GlobalKey}.{global.Key}", global.Value);

            WriteValue(TargetKey, targets.Keys.ToArray());
            foreach (var target in targets)
            {
                string targetKey = $"{TargetKey}.{target.Key}";
                string planKey = $"{PlanKey}.{target.Key}";
                WriteValue(targetKey, target.Value.Keys.ToArray());
                foreach (var entry in target.Value)
                    WriteRecord($"{targetKey}.{entry.Key}", entry.Value);

                var plans = BusPlanStore.GetPlans(target.Key);
                int previousCount = ReadCount(planKey);
                for (int i = 0; i < plans.Count; i++)
                    WriteRecord($"{planKey}.{i}", plans[i].Entry, plans[i].Action);
                for (int i = plans.Count; i < previousCount; i++)
                    ModHelper.RemoveModData(ModId, $"{planKey}.{i}");
                WriteValue(planKey, plans.Count);
            }
        }

        internal static void Clear()
        {
            globals.Clear();
            targets.Clear();
            BusPlanStore.Clear();
        }

        private static T[] ReadIndex<T>(string key) =>
            JsonConvert.DeserializeObject<T[]>(ModHelper.GetModData(ModId, key, "[]")) ?? Array.Empty<T>();

        private static int ReadCount(string key) =>
            JsonConvert.DeserializeObject<int>(ModHelper.GetModData(ModId, key, "0"));

        private static T ReadRecord<T>(string key, out JObject record) where T : class
        {
            string json = ModHelper.GetModData(ModId, key);
            record = json == null ? null : JObject.Parse(json);
            if (record == null) return null;
            string name = record.Value<string>("type");
            if (name == null || !recordTypes.TryGetValue(name, out var type) || !typeof(T).IsAssignableFrom(type))
                throw new JsonSerializationException($"Invalid bus configuration type '{name}' for '{key}'.");
            return (T)record.ToObject(type);
        }

        private static void WriteValue(string key, object value) =>
            ModHelper.SetModData(ModId, key, JsonConvert.SerializeObject(value));

        private static void WriteRecord(string key, object value, string action = null)
        {
            var record = JObject.FromObject(value);
            record["type"] = recordNames[value.GetType()];
            if (action != null) record["action"] = action;
            ModHelper.SetModData(ModId, key, record.ToString(Formatting.None));
        }
    }
}
