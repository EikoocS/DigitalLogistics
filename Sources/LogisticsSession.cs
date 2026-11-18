using DigitalLogistics.Buses;
using DigitalLogistics.Storage;
using DigitalLogistics.Storage.Disks;

namespace DigitalLogistics
{
    internal static class LogisticsSession
    {
        internal static void LoadConfigurations()
        {
            StorageBusManager.Load();
            IOBusManager.Load();
            TriggerBusManager.Load();
        }

        internal static void Stop()
        {
            StorageIndexRefresh.Clear();
            BusController.ClearConnections();
            IOBusManager.Clear();
            TriggerBusManager.Clear();
            StorageBusManager.Clear();
            DigitalLogistics.Manager.Clear();
            SharedProviderRegistry.Clear();
        }

        internal static void Reset()
        {
            Stop();
            StorageDiskStore.Clear();
        }

        internal static void Save()
        {
            StorageDiskStore.Save();
            if (!DigitalLogistics.Instance.Enabled) return;
            StorageBusManager.Save();
            IOBusManager.Save();
            TriggerBusManager.Save();
        }
    }
}
