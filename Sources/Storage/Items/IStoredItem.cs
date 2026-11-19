using DigitalLogistics.Storage;

namespace DigitalLogistics.Storage.Items
{
    public interface IStoredItem
    {
        ItemStorageProvider Provider { get; }
        StorageOutput PrepareOutput();
    }
}
