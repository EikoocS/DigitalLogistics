using DigitalLogistics.Buses;
using DigitalLogistics.Hoppers;
using DigitalLogistics.Item;
using DigitalLogistics.Storage;
using DigitalLogistics.Storage.Disks;
using DigitalLogistics.Tracing;
using DigitalLogistics.UI;

namespace DigitalLogistics
{
    internal static class LogisticsSession
    {
        internal static bool Loading { get; private set; }

        internal static void LoadConfigurations()
        {
            Loading = true;
            HopperSession.BeginLoad();
            BusConfigurationStore.Load();
        }

        internal static void FinishLoad()
        {
            Loading = false;
            StorageDrive.RestoreIndustryDisks();
            HopperSession.FinishLoad();
        }

        internal static void Stop(bool saveIndustry = true)
        {
            PerformanceTrace.Reset();
            StorageIndexRefresh.Clear();
            StorageDrive.ClearConnections(saveIndustry);
            BusController.ClearConnections();
            HopperSession.Clear();
            IOBusManager.Clear();
            FluidIOBusManager.Clear();
            LogisticsScheduler.Clear();
            StorageBusManager.Clear();
            FluidStorageBusManager.Clear();
            DigitalLogistics.Manager.Clear();
            SharedProviderRegistry.Clear();
            FluidProviderRegistry.Clear();
            BusConfigurationStore.Clear();
            Loading = false;
        }

        internal static void Reset()
        {
            ModDataDebugView.Close();
            ModDataDebugEdits.Clear();
            Stop(saveIndustry: false);
            StorageDiskStore.Clear();
        }

        internal static void Save()
        {
            StorageDrive.SaveIndustryDisks();
            StorageDiskStore.Save();
            if (DigitalLogistics.Instance.Enabled)
            {
                BusConfigurationStore.Save();
                HopperSession.Save();
            }
            ModDataDebugEdits.WriteToSave();
        }
    }
}
