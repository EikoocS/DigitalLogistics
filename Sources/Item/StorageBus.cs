using DigitalLogistics.Buses;
using System;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Storage;

namespace DigitalLogistics.Item
{
    internal sealed class StorageBus : InventoryBus
    {
        internal const string ItemId = "storage_bus";
        private readonly Dictionary<ItemDisk, Guid> disks = new();
        internal override IEnumerable<object> Configurations =>
            InventoryLinks.Select(link => StorageBusManager.GetConfiguration(link.Inventory));
        protected override void OnConnected(SharedProviderRegistry.InventoryLink link) => StorageBusManager.Connect(link);
        protected override void OnDisconnected(SharedProviderRegistry.InventoryLink link) => StorageBusManager.Disconnect(link);
        protected override bool HasLinks => base.HasLinks || disks.Count != 0;

        private StorageBus() : base(ItemId) { }
        internal static GameItem Create() => new StorageBus().Item;

        protected override bool CanTargetExternal(GameItem target) =>
            target is ItemDisk || base.CanTargetExternal(target);

        protected override void UpdateLinks(IEnumerable<GameItem> targets)
        {
            base.UpdateLinks(targets.Where(target => target is not ItemDisk));
            var desired = targets.OfType<ItemDisk>().ToHashSet();
            foreach (var disk in disks.Keys.Where(disk => !desired.Contains(disk)).ToArray())
            {
                StorageBusManager.DisconnectDisk(disks[disk]);
                disks.Remove(disk);
            }
            foreach (var disk in desired)
                if (!disks.ContainsKey(disk) && StorageBusManager.ConnectDisk(disk) is Guid id)
                    disks.Add(disk, id);
        }

        protected override void ClearLinks()
        {
            foreach (var id in disks.Values) StorageBusManager.DisconnectDisk(id);
            disks.Clear();
            base.ClearLinks();
        }
    }
}
