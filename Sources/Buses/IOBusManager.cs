using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Item;
using DigitalLogistics.Storage;

namespace DigitalLogistics.Buses
{
    internal static class IOBusManager
    {
        internal sealed class Connection
        {
            internal TransferBusConfiguration Configuration;
            internal GameItem Bus;
            internal SharedProviderRegistry.InventoryLink Link;
            internal LogisticsScheduler.Binding[] Polling;
            internal string DefaultName => Link.Name;
            internal string DefaultFilter => Bus.identifier == IOBus.InputId ? "True" : "False";
        }

        private static readonly Dictionary<GameItem, List<Connection>> active = new();
        internal static IEnumerable<Connection> GetConfigurations(GameItem bus) =>
            active.TryGetValue(bus, out var links) ? links : Enumerable.Empty<Connection>();

        internal static void Acquire(GameItem bus, SharedProviderRegistry.InventoryLink link)
        {
            var configuration = BusConfigurationStore.GetTarget<TransferBusConfiguration>(
                link.Key, BusActions.ForTransfer(bus));
            var connection = new Connection { Configuration = configuration, Bus = bus, Link = link };
            if (!active.TryGetValue(bus, out var links)) active.Add(bus, links = new());
            links.Add(connection);
            connection.Polling = BusController.BindTransfers(bus, configuration, IOBus.CanInput(bus),
                IOBus.CanOutput(bus), fluid: false, () => link.Provider.Available,
                (input, limit) => Transfer(connection, input, limit));
            DigitalLogistics.Manager.NotifyChanged();
        }

        internal static void Release(GameItem bus, GameInventory inventory)
        {
            var links = active[bus];
            var connection = links.Single(row => row.Link.Inventory == inventory);
            foreach (var binding in connection.Polling) binding.Dispose();
            links.Remove(connection);
            if (links.Count == 0) active.Remove(bus);
            DigitalLogistics.Manager.NotifyChanged();
        }

        internal static void Clear()
        {
            foreach (var bus in active.Keys.ToArray())
                foreach (var connection in active[bus].ToArray()) Release(bus, connection.Link.Inventory);
        }

        private static int Transfer(Connection connection, bool input, int limit)
        {
            int remaining = limit;
            var provider = connection.Link.Provider;
            var expression = connection.Configuration.GetExpression(connection.DefaultFilter);
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
            return limit - remaining;
        }
    }
}
