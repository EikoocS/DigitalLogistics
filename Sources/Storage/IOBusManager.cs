using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Controller;
using DigitalLogistics.Filtering;
using DigitalLogistics.Item;
using Newtonsoft.Json;
using UnityEngine;

namespace DigitalLogistics.Storage
{
    internal static class IOBusManager
    {
        private const string DataKey = "io_bus_configuration";
        internal const string OnAddTag = "DIGITAL_LOGISTICS_IO_BUS_ON_ADD";
        internal const string OnMoveTag = "DIGITAL_LOGISTICS_IO_BUS_ON_MOVE";

        [Serializable]
        internal sealed class Configuration
        {
            public string Key;
            public string Name = string.Empty;
            public bool Disabled = true;
            public List<IOBusScheduleEntry> InputSchedule = new();
            public List<IOBusScheduleEntry> OutputSchedule = new();
            public string Filter = string.Empty;
            public int Count = 1;
            [NonSerialized] internal GameItem Bus;
            [NonSerialized] internal BusManager.Link Link;
            [NonSerialized] internal StoredItemExpression Expression;
            [NonSerialized] internal Action<GameItem, GameInventory, SlotMarker> OnAdded;
            internal int RepeatCount => Math.Max(1, Count);
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

        private static readonly Dictionary<string, Configuration> configurations = new();
        private static readonly Dictionary<GameItem, List<Configuration>> active = new();
        private static readonly HashSet<GameItem> pending = new();
        private static Coroutine scheduled;

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
                if (!configuration.Disabled && !SaveManager.IsDecoding && !Bus.IsBus(item) && IsOnAdd(bus)) Schedule(bus);
            };
            link.Inventory.onSlotAddItemEndFunc += configuration.OnAdded;
            if (!active.TryGetValue(bus, out var links)) active.Add(bus, links = new());
            links.Add(configuration);
            DigitalLogistics.Manager.NotifyChanged();
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
            DigitalLogistics.Manager.NotifyChanged();
        }

        internal static void Load()
        {
            string json = ModHelper.GetModData(DigitalLogistics.Instance.Manifest.ID, DataKey);
            if (string.IsNullOrEmpty(json)) return;
            foreach (var configuration in JsonConvert.DeserializeObject<SaveData>(json).Configurations)
                configurations.Add(configuration.Key, configuration);
        }

        internal static void Save()
        {
            var data = new SaveData { Configurations = active.Values.SelectMany(links => links).ToList() };
            ModHelper.SetModData(DigitalLogistics.Instance.Manifest.ID, DataKey, JsonConvert.SerializeObject(data));
        }

        internal static void Clear()
        {
            if (scheduled != null)
                ModHelper.StopCoroutine(DigitalLogistics.Instance.Manifest.ID, scheduled);
            scheduled = null;
            pending.Clear();
            active.Clear();
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
            foreach (var configuration in GetConfigurations(bus))
            {
                if (configuration.Disabled) continue;
                if (IOBus.CanInput(bus))
                    foreach (var operation in GetOperations(configuration, true, nightly)) yield return operation;
                if (IOBus.CanOutput(bus))
                    foreach (var operation in GetOperations(configuration, false, nightly)) yield return operation;
            }
        }

        private static IEnumerable<BusOperation> GetOperations(Configuration configuration, bool input, bool nightly)
        {
            if (!nightly)
            {
                yield return CreateOperation(configuration, input);
                yield break;
            }
            foreach (var entry in input ? configuration.InputSchedule : configuration.OutputSchedule)
                if (entry.Enabled) yield return CreateOperation(configuration, input, entry);
        }

        private static BusOperation CreateOperation(Configuration configuration, bool input, IOBusScheduleEntry entry = null) =>
            new(entry?.Order ?? 6, entry?.Priority ?? 0, input ? BusOperationKind.Input : BusOperationKind.Output,
                () => Transfer(configuration, input, entry));

        private static void Transfer(Configuration configuration, bool input, IOBusScheduleEntry entry)
        {
            if (configuration.Disabled || configuration.Link == null) return;
            var provider = configuration.Link.Provider;
            var expression = entry?.Expression ?? (input && configuration.Bus.identifier == IOBus.InputOutputId
                ? configuration.Expression.Not() : configuration.Expression);
            int remaining = entry?.RepeatCount ?? configuration.RepeatCount;
            var candidates = input ? provider.Snapshot().Where(expression.Evaluate)
                : DigitalLogistics.Manager.SearchForOutput(expression, provider);
            foreach (var stored in candidates)
            {
                using var output = stored.PrepareOutput();
                if (output == null || !CanMove(output.Item, stored.IsDigitalItems())) continue;
                int amount = output.Item.MaxNumRemove();
                int moved = input
                    ? DigitalLogistics.Manager.TryInput(output.Item, amount, target => target != provider).Amount
                    : stored.IsDigitalItems() ? provider.InputFromSystem(output, amount)
                        : provider.InputFromSystem(output.Item, amount).Amount;
                output.Complete(moved);
                if (moved > 0 && --remaining == 0) break;
            }
        }

        private static bool CanMove(GameItem item, bool digital) => !Bus.IsBus(item)
            && GeneralHelper.IsItemOwned(item) && (digital || item.MayRemove()) && item.MaxNumRemove() > 0;
    }
}
