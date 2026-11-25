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

        private static readonly Dictionary<int, BusGlobalConfiguration> globals = new();
        private static readonly Dictionary<string, Dictionary<string, BusTargetConfiguration>> targets = new();
        private static readonly Dictionary<string, Type> recordTypes = new()
        {
            ["global"] = typeof(BusGlobalConfiguration),
            ["storage"] = typeof(StorageBusConfiguration),
            ["item_transfer"] = typeof(TransferBusConfiguration),
            ["fluid_transfer"] = typeof(FluidTransferBusConfiguration)
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
                actions.Add(action, configuration = new T());
            return (T)configuration;
        }

        internal static void Load()
        {
            Clear();
            foreach (int uid in ReadIndex<int>(GlobalKey))
                if (ReadRecord<BusGlobalConfiguration>($"{GlobalKey}.{uid}") is { } settings)
                    globals.Add(uid, settings);

            foreach (string targetId in ReadIndex<string>(TargetKey))
            {
                string targetKey = $"{TargetKey}.{targetId}";
                var actions = new Dictionary<string, BusTargetConfiguration>();
                foreach (string action in ReadIndex<string>(targetKey))
                    if (ReadRecord<BusTargetConfiguration>($"{targetKey}.{action}") is { } configuration)
                        actions.Add(action, configuration);
                if (actions.Count != 0) targets.Add(targetId, actions);
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
                WriteValue(targetKey, target.Value.Keys.ToArray());
                foreach (var entry in target.Value)
                    WriteRecord($"{targetKey}.{entry.Key}", entry.Value);
            }
        }

        internal static void Clear()
        {
            globals.Clear();
            targets.Clear();
        }

        private static T[] ReadIndex<T>(string key) =>
            JsonConvert.DeserializeObject<T[]>(ModHelper.GetModData(ModId, key, "[]")) ?? Array.Empty<T>();

        private static T ReadRecord<T>(string key) where T : class
        {
            string json = ModHelper.GetModData(ModId, key);
            var record = json == null ? null : JObject.Parse(json);
            if (record == null) return null;
            string name = record.Value<string>("type");
            if (name == null || !recordTypes.TryGetValue(name, out var type) || !typeof(T).IsAssignableFrom(type))
                throw new JsonSerializationException($"Invalid bus configuration type '{name}' for '{key}'.");
            return (T)record.ToObject(type);
        }

        private static void WriteValue(string key, object value) =>
            ModHelper.SetModData(ModId, key, JsonConvert.SerializeObject(value));

        private static void WriteRecord(string key, object value)
        {
            var record = JObject.FromObject(value);
            record["type"] = recordNames[value.GetType()];
            ModHelper.SetModData(ModId, key, record.ToString(Formatting.None));
        }
    }
}
