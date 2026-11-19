using DigitalLogistics.Buses;
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
            : base(() => FluidEndpoint.IsContainer(container) && !BusAccess.IsPinnedOrBlocked(container))
        {
            this.container = container;
            Name = name;
            changed = _ => NotifyChanged();
            LiquidChangeEvents.Subscribe(container, changed);
        }

        public override TagState ReadTag(string key) => container.GetTagReadonly(key);
        public override int[] ReadParts() => WaterHelper.GetLiquidParts(container);
        internal override void SetPart(int index, int volumeUL) => container.ModifyTag(
            Liquid.Liquids[index].currentPart, tag => tag.Enable().SetInt(volumeUL), skipValidate: true);
        internal override void ReceiveTemperature(int volumeUL, double temperature) =>
            ChemicalHelper.HandleReceiveLiquidTemp(container, volumeUL, temperature);
        internal override void Synchronize() => container.SyncModifiedState();
        protected override void DisposeCore() => LiquidChangeEvents.Unsubscribe(container, changed);
    }
}
