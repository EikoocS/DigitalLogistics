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
        internal static void LoadConfigurations()
        {
            StorageBusManager.Load();
            IOBusManager.Load();
            FluidStorageBusManager.Load();
            FluidIOBusManager.Load();
            TriggerBusManager.Load();
        }

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
            if (DigitalLogistics.Instance.Enabled)
            {
                StorageBusManager.Save();
                IOBusManager.Save();
                FluidStorageBusManager.Save();
                FluidIOBusManager.Save();
                TriggerBusManager.Save();
            }
            ModDataDebugEdits.WriteToSave();
        }
    }
}
