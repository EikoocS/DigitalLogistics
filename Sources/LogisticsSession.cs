using DigitalLogistics.Buses;
using DigitalLogistics.Item;
using DigitalLogistics.Storage;
using DigitalLogistics.Storage.Disks;
using DigitalLogistics.Tracing;
using DigitalLogistics.UI;

namespace DigitalLogistics
{
    internal static class LogisticsSession
    {
        internal static void LoadConfigurations() => BusConfigurationStore.Load();

        internal static void Stop()
        {
            PerformanceTrace.Reset();
            StorageIndexRefresh.Clear();
            BusController.ClearConnections();
            StorageDrive.ClearConnections();
            IOBusManager.Clear();
            FluidIOBusManager.Clear();
            TriggerBusManager.Clear();
            StorageBusManager.Clear();
            FluidStorageBusManager.Clear();
            DigitalLogistics.Manager.Clear();
            SharedProviderRegistry.Clear();
            FluidProviderRegistry.Clear();
            BusConfigurationStore.Clear();
        }

        internal static void Reset()
        {
            ModDataDebugView.Close();
            ModDataDebugEdits.Clear();
            Stop();
            StorageDiskStore.Clear();
        }

        internal static void Save()
        {
            StorageDiskStore.Save();
            if (DigitalLogistics.Instance.Enabled) BusConfigurationStore.Save();
            ModDataDebugEdits.WriteToSave();
        }
    }
}
