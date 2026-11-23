using DigitalLogistics.Buses;
using DigitalLogistics.Item;
using DigitalLogistics.Storage.Items;

namespace DigitalLogistics.Storage.Providers
{
    public sealed class ContainerStorageProvider : FluidStorageProvider
    {
        private readonly GameItem container;
        private readonly System.Action<bool> changed;
        internal override GameItem PhysicalContainer => container;
        public override CachedItem Content => new PhysicalItem(container);

        public ContainerStorageProvider(GameItem container, string name = "Unnamed")
            : base(() => FluidEndpoint.IsContainer(container) && !ItemAccess.IsPinnedOrBlocked(container))
        {
            this.container = container;
            Name = name;
            changed = _ => NotifyChanged();
            LiquidChangeEvents.Subscribe(container, changed);
        }

        public override TagState ReadTag(string key) => container.GetTagReadonly(key);
        public override long[] ReadParts() => System.Array.ConvertAll(WaterHelper.GetLiquidParts(container), part => (long)part);
        internal override void SetPart(int index, long volumeUL) => container.ModifyTag(
            Liquid.Liquids[index].currentPart, tag => tag.Enable().SetInt((int)volumeUL), skipValidate: true);
        internal override void ReceiveTemperature(int volumeUL, double temperature) =>
            ChemicalHelper.HandleReceiveLiquidTemp(container, volumeUL, temperature);
        internal override void Synchronize() => container.SyncModifiedState();
        protected override void DisposeCore() => LiquidChangeEvents.Unsubscribe(container, changed);
    }
}
