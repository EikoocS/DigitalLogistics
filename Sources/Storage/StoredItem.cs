namespace DigitalStorage.Storage
{
    public interface StoredItem
    {
        public abstract string DisplayName { get; }
        public abstract long Price { get; }
        public abstract int? Heat { get; }
        public abstract int ContrabandLevel { get; }
    }
}
