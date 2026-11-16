using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DigitalStorage.Filtering;
using DigitalStorage.Item;
using UnityEngine;

namespace DigitalStorage.Storage
{
    internal static class IOBusManager
    {
        private const string DataKey = "io_bus_configuration";
        internal const string OnAddTag = "DIGITAL_STORAGE_IO_BUS_ON_ADD";
        internal const string OnMoveTag = "DIGITAL_STORAGE_IO_BUS_ON_MOVE";

        [Serializable]
        internal sealed class Configuration
        {
            public string Key;
            public string Name = string.Empty;
            public bool Disabled = true;
            public int InputOrder = 6;
            public int OutputOrder = 6;
            public int InputPriority;
            public int OutputPriority;
            public string Filter = string.Empty;
            [NonSerialized] internal GameItem Bus;
            [NonSerialized] internal BusManager.Link Link;
            [NonSerialized] internal StoredItemExpression Expression;
            [NonSerialized] internal Action<GameItem, GameInventory, SlotMarker> OnAdded;
            internal string DefaultName => Link.Name;
            internal string DefaultFilter => Bus.identifier == IOBus.InputId ? "True" : "False";

            internal void Apply() => Expression = StoredItemExpressionParser.Parse(
                string.IsNullOrWhiteSpace(Filter) ? DefaultFilter : Filter);
        }

        [Serializable]
        private sealed class SaveData
        {
            public List<Configuration> Configurations = new();
        }

        private readonly struct Operation
        {
            internal readonly Configuration Configuration;
            internal readonly bool Input;
            internal int Order => Input ? Configuration.InputOrder : Configuration.OutputOrder;
            internal int Priority => Input ? Configuration.InputPriority : Configuration.OutputPriority;

            internal Operation(Configuration configuration, bool input)
            {
                Configuration = configuration;
                Input = input;
            }
        }

        private static readonly Dictionary<string, Configuration> configurations = new();
        private static readonly Dictionary<GameItem, List<Configuration>> active = new();
        private static readonly HashSet<GameItem> pending = new();
        private static Coroutine scheduled;
        private static bool running;

        internal static bool IsOnAdd(GameItem bus) => bus.GetTagReadonly(OnAddTag).GetBool();
        internal static bool IsOnMove(GameItem bus) => bus.GetTagReadonly(OnMoveTag).GetBool();

        internal static IEnumerable<Configuration> GetConfigurations(GameItem bus) =>
            active.TryGetValue(bus, out var links) ? links : Enumerable.Empty<Configuration>();

        internal static void Acquire(GameItem bus, BusManager.Link link)
        {
            string key = bus.GetUniqueID() + ":" + link.Key;
            if (!configurations.TryGetValue(key, out var configuration))
            {
                configuration = new Configuration { Key = key };
                configurations.Add(key, configuration);
            }
            configuration.Bus = bus;
            configuration.Link = link;
            configuration.Apply();
            configuration.OnAdded = (item, _, _) =>
            {
                if (!configuration.Disabled && !SaveManager.IsDecoding && !IOBus.IsBus(item) && IsOnAdd(bus)) Schedule(bus);
            };
            link.Inventory.onSlotAddItemEndFunc += configuration.OnAdded;
            if (!active.TryGetValue(bus, out var links)) active.Add(bus, links = new());
            links.Add(configuration);
        }

        internal static void Release(GameItem bus, GameInventory inventory)
        {
            var links = active[bus];
            var configuration = links.Single(config => config.Link.Inventory == inventory);
            inventory.onSlotAddItemEndFunc -= configuration.OnAdded;
            links.Remove(configuration);
            configuration.Link = null;
            configuration.Bus = null;
            configuration.OnAdded = null;
            if (links.Count == 0)
            {
                active.Remove(bus);
                pending.Remove(bus);
            }
        }

        internal static void Load()
        {
            string json = ModHelper.GetModData(DigitalStorage.Instance.Manifest.ID, DataKey);
            if (string.IsNullOrEmpty(json)) return;
            foreach (var configuration in JsonUtility.FromJson<SaveData>(json).Configurations)
                configurations.Add(configuration.Key, configuration);
        }

        internal static void Save()
        {
            var data = new SaveData { Configurations = active.Values.SelectMany(links => links).ToList() };
            ModHelper.SetModData(DigitalStorage.Instance.Manifest.ID, DataKey, JsonUtility.ToJson(data));
        }

        internal static void Clear()
        {
            if (scheduled != null && OverlayHandler.current != null)
                OverlayHandler.current.StopCoroutine(scheduled);
            scheduled = null;
            pending.Clear();
            active.Clear();
            configurations.Clear();
        }

        internal static void Schedule(GameItem bus)
        {
            if (running || IsAway() || !active.ContainsKey(bus)) return;
            pending.Add(bus);
            if (scheduled == null && OverlayHandler.current != null)
                scheduled = OverlayHandler.current.StartCoroutine(RunPending());
        }

        private static IEnumerator RunPending()
        {
            yield return null;
            scheduled = null;
            var buses = pending.ToArray();
            pending.Clear();
            Execute(GetOperations(buses));
        }

        internal static void Run(GameItem bus) => Execute(GetOperations(new[] { bus }));

        internal static void RunWindow(int window) => Execute(GetOperations(active.Keys)
            .Where(operation => HopperHelper.WindowOf(operation.Order) == window));

        private static IEnumerable<Operation> GetOperations(IEnumerable<GameItem> buses)
        {
            foreach (var bus in buses)
                foreach (var configuration in GetConfigurations(bus))
                {
                    if (configuration.Disabled) continue;
                    if (IOBus.CanInput(bus)) yield return new Operation(configuration, true);
                    if (IOBus.CanOutput(bus)) yield return new Operation(configuration, false);
                }
        }

        private static bool IsAway() => MapUIManager.Instance != null && MapUIManager.Instance.IsOutside;

        private static void Execute(IEnumerable<Operation> operations)
        {
            if (running || IsAway() || !DigitalStorage.Instance.Enabled) return;
            var ordered = operations.OrderBy(operation => operation.Order)
                .ThenBy(operation => operation.Input ? 0 : 1)
                .ThenByDescending(operation => operation.Priority).ToArray();
            running = true;
            try
            {
                foreach (var operation in ordered)
                {
                    var configuration = operation.Configuration;
                    if (configuration.Link == null || configuration.Bus.IsTag(StorageBus.DisabledTag)
                        || !GeneralHelper.IsItemOwned(configuration.Bus)) continue;
                    if (operation.Input) Input(configuration);
                    else Output(configuration);
                }
            }
            finally { running = false; }
        }

        private static bool CanMove(CachedItem stored)
        {
            if (!stored.VerifyValidity()) return false;
            var item = stored.GetItemInstance();
            return !IOBus.IsBus(item)
                && GeneralHelper.IsItemOwned(item) && item.MayRemove() && item.MaxNumRemove() > 0;
        }

        private static void Input(Configuration configuration)
        {
            var expression = configuration.Bus.identifier == IOBus.InputOutputId
                ? configuration.Expression.Not() : configuration.Expression;
            foreach (var stored in configuration.Link.Provider.Snapshot())
            {
                if (!expression.Evaluate(stored) || !CanMove(stored)) continue;
                using var output = stored.PrepareOutput();
                if (output == null) continue;
                var result = DigitalStorage.Manager.TryInput(output.Item, output.Item.MaxNumRemove(),
                    provider => provider != configuration.Link.Provider);
                output.Complete(result.Amount);
            }
        }

        private static void Output(Configuration configuration)
        {
            foreach (var stored in DigitalStorage.Manager.SearchForOutput(
                configuration.Expression, configuration.Link.Provider))
            {
                if (!CanMove(stored)) continue;
                using var output = stored.PrepareOutput();
                if (output == null) continue;
                var result = configuration.Link.Provider.InputFromSystem(output.Item, output.Item.MaxNumRemove());
                output.Complete(result.Amount);
            }
        }
    }
}
