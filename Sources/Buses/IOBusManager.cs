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
    internal static class IOBusManager
    {
        private const string DataKey = "io_bus_configuration";
        internal const string OnAddTag = "DIGITAL_LOGISTICS_IO_BUS_ON_ADD";
        internal const string OnMoveTag = "DIGITAL_LOGISTICS_IO_BUS_ON_MOVE";

        [Serializable]
        internal sealed class Configuration : TransferBusConfiguration<IOBusScheduleEntry> { }

        internal sealed class Connection
        {
            internal Configuration Configuration;
            internal GameItem Bus;
            internal SharedProviderRegistry.InventoryLink Link;
            internal CachedItemExpression Expression;
            internal Action<GameItem, GameInventory, SlotMarker> OnAdded;
            internal string DefaultName => Link.Name;
            internal string DefaultFilter => Bus.identifier == IOBus.InputId ? "True" : "False";
        }

        [Serializable]
        private sealed class SaveData
        {
            public List<Configuration> Configurations = new();
        }

        private static readonly Dictionary<string, Configuration> configurations = new();
        private static readonly Dictionary<GameItem, List<Connection>> active = new();
        private static readonly HashSet<GameItem> pending = new();
        private static Coroutine scheduled;

        internal static bool IsOnAdd(GameItem bus) => bus.GetTagReadonly(OnAddTag).GetBool();
        internal static bool IsOnMove(GameItem bus) => bus.GetTagReadonly(OnMoveTag).GetBool();
        internal static IEnumerable<Connection> GetConfigurations(GameItem bus) =>
            active.TryGetValue(bus, out var links) ? links : Enumerable.Empty<Connection>();

        internal static void ApplyConfiguration(Connection connection) => connection.Expression =
            CachedItemExpressionParser.Parse(string.IsNullOrWhiteSpace(connection.Configuration.Filter)
                ? connection.DefaultFilter : connection.Configuration.Filter);

        internal static void Acquire(GameItem bus, SharedProviderRegistry.InventoryLink link)
        {
            string key = bus.GetUniqueID() + ":" + link.Key;
            if (!configurations.TryGetValue(key, out var configuration))
                configurations.Add(key, configuration = new Configuration { Key = key });
            var connection = new Connection { Configuration = configuration, Bus = bus, Link = link };
            ApplyConfiguration(connection);
            connection.OnAdded = (item, _, _) =>
            {
                if (!configuration.Disabled && !SaveManager.IsDecoding && !Bus.IsBus(item) && IsOnAdd(bus)) Schedule(bus);
            };
            link.Inventory.onSlotAddItemEndFunc += connection.OnAdded;
            if (!active.TryGetValue(bus, out var links)) active.Add(bus, links = new());
            links.Add(connection);
            DigitalLogistics.Manager.NotifyChanged();
        }

        internal static void Release(GameItem bus, GameInventory inventory)
        {
            var links = active[bus];
            var connection = links.Single(row => row.Link.Inventory == inventory);
            inventory.onSlotAddItemEndFunc -= connection.OnAdded;
            links.Remove(connection);
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

        internal static void Save() => ModHelper.SetModData(DigitalLogistics.Instance.Manifest.ID, DataKey,
            JsonConvert.SerializeObject(new SaveData
            {
                Configurations = active.Values.SelectMany(links => links).Select(row => row.Configuration).ToList()
            }));

        internal static void Clear()
        {
            if (scheduled != null) ModHelper.StopCoroutine(DigitalLogistics.Instance.Manifest.ID, scheduled);
            scheduled = null;
            pending.Clear();
            foreach (var bus in active.Keys.ToArray())
                foreach (var connection in active[bus].ToArray()) Release(bus, connection.Link.Inventory);
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
                foreach (var operation in BusOperation.ForTransfers(IOBus.CanInput(bus), IOBus.CanOutput(bus),
                    nightly ? connection.Configuration.Schedule : null,
                    DigitalLogistics.Instance.Settings.MaxContinuousItemTransfers.Value, entry => entry.Count,
                    (bool input, IOBusScheduleEntry entry, ref int remaining) => Transfer(connection, input, entry, ref remaining)))
                    yield return operation;
            }
        }

        private static void Transfer(Connection connection, bool input, IOBusScheduleEntry entry, ref int remaining)
        {
            if (remaining <= 0) return;
            var configuration = connection.Configuration;
            if (configuration.Disabled || !GetConfigurations(connection.Bus).Contains(connection)) return;
            var provider = connection.Link.Provider;
            if (!provider.Available) return;
            var expression = entry?.Expression ?? connection.Expression;
            if (input && connection.Bus.identifier == IOBus.InputOutputId) expression = expression.Not();
            var candidates = input ? provider.Snapshot().Where(expression.Evaluate)
                : DigitalLogistics.Manager.SearchForOutput(expression, provider);
            foreach (var stored in candidates)
            {
                using (var lease = stored.AcquireInstance())
                    if (lease == null || Bus.IsBus(lease.Item) || BusAccess.IsBlocked(lease.Item)) continue;
                var result = input
                    ? StorageTransfer.TryInput(stored, remaining, providerFilter: target => target != provider)
                    : StorageTransfer.TryInput(stored, provider, remaining);
                remaining -= result.Amount;
                if (remaining == 0) break;
            }
        }
    }
}
