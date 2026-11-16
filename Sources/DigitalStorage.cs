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
            ModHook.OnGameLoadedInit += StorageBusController.ClearConnections;
            ModHook.OnGameLoadedEarly += StorageBusManager.Load;
            Log.Log("Enabled.");
        }

        public void OnDisable()
        {
            Enabled = false;
            ModHelper.RemoveDebugFunctions(Manifest.ID);
            DebugMenu.Close();
            ModHook.OnModItemDirectoryInit -= RegisterItems;
            ModHook.OnGameLoadedInit -= StorageBusController.ClearConnections;
            ModHook.OnGameLoadedEarly -= StorageBusManager.Load;
            StorageBusController.ClearConnections();
            Log.Log("Disabled.");
        }

        private void RegisterItems(ModItemDirectory directory)
        {
            directory.Add(StorageTerminal.ItemId, StorageTerminal.Create);
            directory.Add(StorageBus.ItemId, StorageBus.Create);
        }
    }
}
