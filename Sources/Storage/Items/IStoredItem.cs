using DigitalLogistics.Storage;

namespace DigitalLogistics.Storage.Items
{
    public interface IStoredItem
    {
        StorageProvider Provider { get; }
        StorageOutput PrepareOutput();
    }
}
