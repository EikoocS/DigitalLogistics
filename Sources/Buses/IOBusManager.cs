using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Item;
using DigitalLogistics.Storage;
using DigitalLogistics.Tracing;
using UnityEngine;

namespace DigitalLogistics.Buses
{
    internal static class IOBusManager
    {
        internal sealed class Connection
        {
            internal TransferBusConfiguration<IOBusScheduleEntry> Configuration;
            internal GameItem Bus;
            internal SharedProviderRegistry.InventoryLink Link;
            internal Action<GameItem, GameInventory, SlotMarker> OnAdded;
            internal string DefaultName => Link.Name;
            internal string DefaultFilter => Bus.identifier == IOBus.InputId ? "True" : "False";
        }

        private static readonly Dictionary<GameItem, List<Connection>> active = new();
        private static readonly HashSet<GameItem> pending = new();
        private static Coroutine scheduled;

        internal static bool IsOnAdd(GameItem bus) => BusConfigurationStore.FindGlobal(bus)?.OnAdd == true;
        internal static bool IsOnMove(GameItem bus) => BusConfigurationStore.FindGlobal(bus)?.OnMove == true;
        internal static IEnumerable<Connection> GetConfigurations(GameItem bus) =>
            active.TryGetValue(bus, out var links) ? links : Enumerable.Empty<Connection>();

        internal static void Acquire(GameItem bus, SharedProviderRegistry.InventoryLink link)
        {
            var configuration = BusConfigurationStore.GetTarget<TransferBusConfiguration<IOBusScheduleEntry>>(
                link.Key, BusActions.ForTransfer(bus));
            var connection = new Connection { Configuration = configuration, Bus = bus, Link = link };
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

        internal static void Clear()
        {
            if (scheduled != null) ModHelper.StopCoroutine(DigitalLogistics.Instance.Manifest.ID, scheduled);
            scheduled = null;
            pending.Clear();
            foreach (var bus in active.Keys.ToArray())
                foreach (var connection in active[bus].ToArray()) Release(bus, connection.Link.Inventory);
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
            BusController.Run(buses, BusRunSource.Automatic);
        }

        internal static IEnumerable<BusOperation> GetOperations(GameItem bus, bool nightly)
        {
            foreach (var connection in GetConfigurations(bus))
            {
                var configuration = connection.Configuration;
                if (configuration.Disabled) continue;
                foreach (var operation in BusOperation.ForTransfers(configuration, IOBus.CanInput(bus), IOBus.CanOutput(bus),
                    nightly ? BusPlanStore.GetPlans(configuration) : null,
                    DigitalLogistics.Instance.Settings.MaxContinuousItemTransfers.Value, entry => entry.Count,
                    () => !configuration.Disabled && GetConfigurations(bus).Contains(connection) && connection.Link.Provider.Available,
                    (bool input, IOBusScheduleEntry entry, ref int remaining) => Transfer(connection, input, entry, ref remaining)))
                    yield return operation;
            }
        }

        private static void Transfer(Connection connection, bool input, IOBusScheduleEntry entry, ref int remaining)
        {
            var provider = connection.Link.Provider;
            var expression = entry?.Expression ?? connection.Configuration.GetExpression(connection.DefaultFilter);
            if (input && connection.Bus.identifier == IOBus.InputOutputId) expression = expression.Not();
            var candidates = input ? provider.Snapshot().Where(expression.Evaluate)
                : DigitalLogistics.Manager.SearchForOutput(expression, provider);
            foreach (var stored in candidates)
            {
                using (var lease = stored.AcquireInstance())
                    if (lease == null || Bus.IsBus(lease.Item) || ItemAccess.IsBlocked(lease.Item)) continue;
                var result = input
                    ? StorageTransfer.TryInput(stored, remaining, providerFilter: target => target != provider)
                    : StorageTransfer.TryInput(stored, provider, remaining);
                remaining -= result.Amount;
                if (remaining == 0) break;
            }
        }
    }
}
