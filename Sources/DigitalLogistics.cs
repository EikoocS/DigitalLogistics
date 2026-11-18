using DigitalLogistics.Controller;
using DigitalLogistics.Item;
using DigitalLogistics.Storage;

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

        public void Init(ModManifest manifest)
        {
            Instance = this;
            Manifest = manifest;
            Settings = new ModConfiguration(manifest.ID);
            Manager = new StorageManager();
            Log = new ModLog(manifest);
            Log.Log($"{manifest.Name} v{manifest.ModVersion} loaded.");
        }

        public void OnEnable()
        {
            Enabled = true;
            ModHelper.AddDebugFunction(Manifest.ID, CustomUIDebugPanel.CATEGORY_SPECIAL,
                "DigitalLogistics", DebugMenu.Open, tall: true);
            ModHook.OnModItemDirectoryInit += RegisterItems;
            ModHook.OnCreateTooltipLate += ItemDisk.AddTooltip;
            ModHook.OnGameLoadedInit += BusController.ClearConnections;
            ModHook.OnGameLoadedInit += StorageDiskStore.Clear;
            ModHook.OnGameLoadedEarly += BusManager.Load;
            ModHook.OnGameLoadedEarly += IOBusManager.Load;
            ModHook.OnGameLoadedEarly += TriggerBusManager.Load;
            Log.Log("Enabled.");
        }

        public void OnDisable()
        {
            Enabled = false;
            ModHelper.RemoveDebugFunctions(Manifest.ID);
            DebugMenu.Close();
            ModHook.OnModItemDirectoryInit -= RegisterItems;
            ModHook.OnCreateTooltipLate -= ItemDisk.AddTooltip;
            ModHook.OnGameLoadedInit -= BusController.ClearConnections;
            ModHook.OnGameLoadedInit -= StorageDiskStore.Clear;
            ModHook.OnGameLoadedEarly -= BusManager.Load;
            ModHook.OnGameLoadedEarly -= IOBusManager.Load;
            ModHook.OnGameLoadedEarly -= TriggerBusManager.Load;
            BusController.ClearConnections();
            Log.Log("Disabled.");
        }

        private void RegisterItems(ModItemDirectory directory)
        {
            directory.Add(StorageTerminal.ItemId, StorageTerminal.Create);
            directory.Add(ItemDisk.ItemId, ItemDisk.Create);
            directory.Add(StorageBus.ItemId, StorageBus.Create);
            directory.Add(TriggerBus.ItemId, TriggerBus.Create);
            directory.Add(IOBus.InputId, () => IOBus.Create(IOBus.InputId));
            directory.Add(IOBus.OutputId, () => IOBus.Create(IOBus.OutputId));
            directory.Add(IOBus.InputOutputId, () => IOBus.Create(IOBus.InputOutputId));
        }
    }
}
