using DigitalLogistics.Item;
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
            ItemDiskStorageProvider.Blacklist.Add(item => item is StorageDisk || Bus.IsBus(item));
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
            StorageIndexRefresh.Completed -= StorageTerminalUI.NotifyRefreshResult;
            LogisticsSession.Stop();
            Log.Log("Disabled.");
        }

        private void RegisterItems(ModItemDirectory directory)
        {
            directory.Add(StorageTerminal.ItemId, StorageTerminal.Create);
            directory.Add(ItemDisk.ItemId, ItemDisk.Create);
            directory.Add(FluidDisk.ItemId, FluidDisk.Create);
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
