using DigitalStorage.Controller;
using DigitalStorage.Item;
using DigitalStorage.Storage;

namespace DigitalStorage
{
    public class DigitalStorage : IMod
    {
        public static DigitalStorage Instance { get; private set; }
        public ModManifest Manifest { get; private set; }
        public ModLog Log { get; private set; }
        public static StorageManager Manager { get; private set; }
        internal bool Enabled { get; private set; }

        public void Init(ModManifest manifest)
        {
            Instance = this;
            Manifest = manifest;
            Manager = new StorageManager();
            Log = new ModLog(manifest);
            Log.Log($"{manifest.Name} v{manifest.ModVersion} loaded.");
        }

        public void OnEnable()
        {
            Enabled = true;
            ModHelper.AddDebugFunction(Manifest.ID, CustomUIDebugPanel.CATEGORY_SPECIAL,
                "DigitalStorage", DebugMenu.Open, tall: true);
            ModHook.OnModItemDirectoryInit += RegisterItems;
            ModHook.OnGameLoadedInit += BusController.ClearConnections;
            ModHook.OnGameLoadedEarly += BusManager.Load;
            ModHook.OnGameLoadedEarly += IOBusManager.Load;
            Log.Log("Enabled.");
        }

        public void OnDisable()
        {
            Enabled = false;
            ModHelper.RemoveDebugFunctions(Manifest.ID);
            DebugMenu.Close();
            ModHook.OnModItemDirectoryInit -= RegisterItems;
            ModHook.OnGameLoadedInit -= BusController.ClearConnections;
            ModHook.OnGameLoadedEarly -= BusManager.Load;
            ModHook.OnGameLoadedEarly -= IOBusManager.Load;
            BusController.ClearConnections();
            Log.Log("Disabled.");
        }

        private void RegisterItems(ModItemDirectory directory)
        {
            directory.Add(StorageTerminal.ItemId, StorageTerminal.Create);
            directory.Add(StorageBus.ItemId, StorageBus.Create);
            directory.Add(IOBus.InputId, () => IOBus.Create(IOBus.InputId));
            directory.Add(IOBus.OutputId, () => IOBus.Create(IOBus.OutputId));
            directory.Add(IOBus.InputOutputId, () => IOBus.Create(IOBus.InputOutputId));
        }
    }
}
