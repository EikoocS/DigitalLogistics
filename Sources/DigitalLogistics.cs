using DigitalLogistics.Item;
using DigitalLogistics.Patch;
using DigitalLogistics.Storage;
using DigitalLogistics.Storage.Providers;
using DigitalLogistics.UI;

namespace DigitalLogistics
{
    public class DigitalLogistics : IMod
    {
        public static DigitalLogistics Instance { get; private set; }
        public ModManifest Manifest { get; private set; }
        public ModLog Log { get; private set; }
        public static StorageManager Manager { get; private set; }
        internal bool Enabled { get; private set; }
        internal ModConfiguration Settings { get; private set; }

        internal static string Localize(string key) => ModHelper.GetLocalized(Instance.Manifest.ID, key);

        public void Init(ModManifest manifest)
        {
            Instance = this;
            Manifest = manifest;
            Settings = new ModConfiguration(manifest.ID);
            Manager = new StorageManager();
            SuppressItemClickActivation.Register(Bus.IsBus);
            SuppressItemClickActivation.Register(item => item is Hopper);
            ItemDiskStorageProvider.Blacklist.Add(item => item is StorageDisk or StorageDrive or Hopper
                || item.identifier == StorageTerminal.ItemId || Bus.IsBus(item));
            Log = new ModLog(manifest);
            Log.Log($"{manifest.Name} v{manifest.ModVersion} loaded.");
        }

        public void OnEnable()
        {
            Enabled = true;
            ModHelper.AddDebugFunction(Manifest.ID, CustomUIDebugPanel.CATEGORY_SPECIAL,
                "DigitalLogistics", DebugMenu.Open, tall: true);
            ModHook.OnModItemDirectoryInit += RegisterItems;
            ModHook.OnCreateTooltipLate += StorageDisk.AddTooltip;
            ModHook.OnGameLoadedInit += LogisticsSession.Reset;
            ModHook.OnGameLoadedEarly += LogisticsSession.LoadConfigurations;
            ModHook.OnGameLoadedLate += LogisticsSession.FinishLoad;
            StorageIndexRefresh.Completed += StorageTerminalUI.NotifyRefreshResult;
            Log.Log("Enabled.");
        }

        public void OnDisable()
        {
            Enabled = false;
            ModHelper.RemoveDebugFunctions(Manifest.ID);
            DebugMenu.Close();
            ModHook.OnModItemDirectoryInit -= RegisterItems;
            ModHook.OnCreateTooltipLate -= StorageDisk.AddTooltip;
            ModHook.OnGameLoadedInit -= LogisticsSession.Reset;
            ModHook.OnGameLoadedEarly -= LogisticsSession.LoadConfigurations;
            ModHook.OnGameLoadedLate -= LogisticsSession.FinishLoad;
            StorageIndexRefresh.Completed -= StorageTerminalUI.NotifyRefreshResult;
            LogisticsSession.Stop();
            Log.Log("Disabled.");
        }

        private void RegisterItems(ModItemDirectory directory)
        {
            directory.Add(StorageTerminal.ItemId, StorageTerminal.Create);
            StorageDisk.RegisterTiers(directory, ItemDisk.FamilyId, ItemDisk.Create);
            StorageDisk.RegisterTiers(directory, FluidDisk.FamilyId, FluidDisk.Create);
            StorageDrive.RegisterVariants(directory);
            Hopper.Register(directory);
            directory.Add(FluidStorageBus.ItemId, FluidStorageBus.Create);
            directory.Add(FluidIOBus.InputId, () => FluidIOBus.Create(FluidIOBus.InputId));
            directory.Add(FluidIOBus.OutputId, () => FluidIOBus.Create(FluidIOBus.OutputId));
            directory.Add(FluidIOBus.InputOutputId, () => FluidIOBus.Create(FluidIOBus.InputOutputId));
            directory.Add(StorageBus.ItemId, StorageBus.Create);
            directory.Add(TriggerBus.ItemId, TriggerBus.Create);
            directory.Add(IOBus.InputId, () => IOBus.Create(IOBus.InputId));
            directory.Add(IOBus.OutputId, () => IOBus.Create(IOBus.OutputId));
            directory.Add(IOBus.InputOutputId, () => IOBus.Create(IOBus.InputOutputId));
        }
    }
}
