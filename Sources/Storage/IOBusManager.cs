using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Controller;
using DigitalLogistics.Filtering;
using DigitalLogistics.Item;
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
            public List<BusScheduleEntry> InputSchedule = new();
            public List<BusScheduleEntry> OutputSchedule = new();
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
            foreach (var configuration in JsonUtility.FromJson<SaveData>(json).Configurations)
                configurations.Add(configuration.Key, configuration);
        }

        internal static void Save()
        {
            var data = new SaveData { Configurations = active.Values.SelectMany(links => links).ToList() };
            ModHelper.SetModData(DigitalLogistics.Instance.Manifest.ID, DataKey, JsonUtility.ToJson(data));
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
                if (entry.Enabled) yield return CreateOperation(configuration, input, entry.Order, entry.Priority);
        }

        private static BusOperation CreateOperation(Configuration configuration, bool input, int order = 6,
            int priority = 0) => new(order, priority, input ? BusOperationKind.Input : BusOperationKind.Output, () =>
            {
                if (configuration.Link == null) return;
                if (input) Input(configuration);
                else Output(configuration);
            });

        private static bool CanMove(CachedItem stored)
        {
            if (!stored.VerifyValidity()) return false;
            var item = stored.GetItemInstance();
            return !Bus.IsBus(item)
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
                var result = DigitalLogistics.Manager.TryInput(output.Item, output.Item.MaxNumRemove(),
                    provider => provider != configuration.Link.Provider);
                output.Complete(result.Amount);
            }
        }

        private static void Output(Configuration configuration)
        {
            foreach (var stored in DigitalLogistics.Manager.SearchForOutput(
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
