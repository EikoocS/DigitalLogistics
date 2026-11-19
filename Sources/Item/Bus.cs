using DigitalLogistics.Buses;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.UI.Buses;
using UnityEngine;

namespace DigitalLogistics.Item
{
    internal abstract class Bus
    {
        internal GameItem Item { get; }
        protected GameInventory Parent { get; private set; }
        protected bool IsInternal => GetMode(Item) == BusMode.Internal;
        internal bool Disabled => GetMode(Item) == BusMode.Disabled;
        internal bool CanExecute => !Disabled && HasLinks && !BusAccess.IsBlocked(Item);
        protected abstract bool HasLinks { get; }
        internal abstract IEnumerable<object> Configurations { get; }

        protected Bus(string id)
        {
            Item = ItemDirectory.CreateEmptyItem(id)
                .SetSpriteAndShapeFromMod(DigitalLogistics.Instance.Manifest.ID, id)
                .SetShape(new GridShapeBuilder().SetDataFill(3, 1));
            Item.SetContentWindow(BusUI.Create(Item));
            RefreshWindowState();
            Item.SetGameItemType("MACHINE");
            Item.EnableTag(GeneralHelper.IS_UNLOAD_DISABLED_TAG);
            Item.shortDescription = DigitalLogistics.Localize($"item.{id}.desc");
            Item.flavorText = DigitalLogistics.Localize($"item.{id}.flavor");
            Item.mayToggleSlotItemFunc = (_, _, _) => true;
            Item.canToggleSlotItemFunc = (_, _, _) => true;
            Item.onToggleSlotItemFunc += (_, _, _) => Toggle();
            Item.onSlotAddItemFunc += (_, inventory, _) =>
            {
                Connect(inventory);
                if (!SaveManager.IsDecoding) OnMoved();
            };
            Item.onSlotRemoveItemFunc += (_, _, _) => Disconnect();
            Item.onLoaded += _ =>
            {
                UpdateAppearance();
                if (Item.parentInventory != null) Connect(Item.parentInventory);
            };
            Item.mayActivateSlotItemFunc = (_, _, _) => this is ITriggerableBus;
            Item.canActivateSlotItemFunc = (_, _, _) => CanExecute;
            Item.onActivateSlotItemFunc = (_, _, _) => BusController.Run(new[] { Item });
            Item.EnableTag(GeneralHelper.CONFIGURE_TAG);
            Item.onConfigureSlotItemFunc = (_, _, _) =>
            {
                Refresh();
                Item.contentWindow.Show();
            };
            Item.getItemSpriteModifierForItemFunc = self => new List<ItemSpriteModifier>
            {
                new(((GameItemElement)self).ResolveSpriteByName(GetStatusSpriteId()), Vector2.zero)
            };
            UpdateAppearance();
        }

        internal static bool IsBus(GameItem item) => item?.identifier is StorageBus.ItemId
            or IOBus.InputId or IOBus.OutputId or IOBus.InputOutputId or TriggerBus.ItemId
            or FluidStorageBus.ItemId or FluidIOBus.InputId or FluidIOBus.OutputId or FluidIOBus.InputOutputId;

        internal static bool ContainsEnabledBus(GameItem item)
        {
            static bool IsEnabledBus(GameItem candidate) => IsBus(candidate) && GetMode(candidate) != BusMode.Disabled;

            return IsEnabledBus(item) || GraphUtils.FindChildType<GameItem>(item, IsEnabledBus, _ => true) != null;
        }

        internal static void SuppressClickActivation(GameItem item, ref bool activate)
        {
            if (IsBus(item)) activate = false;
        }

        private string GetStatusSpriteId()
        {
            string family = FluidIOBus.IsIO(Item) || Item.identifier == FluidStorageBus.ItemId ? "fluid_bus" : "bus";
            return $"{family}_status_{Mode}";
        }

        private static BusMode GetMode(GameItem item) => BusConfigurationStore.FindGlobal(item)?.Mode ?? BusMode.External;

        private string Mode => GetMode(Item) switch
        {
            BusMode.Disabled => "disabled",
            BusMode.Internal => "internal",
            _ => "external"
        };

        private void UpdateAppearance()
        {
            Item.SetSprite(DigitalLogistics.Instance.Manifest.ID, Item.identifier);
            Item.name = DigitalLogistics.Localize($"item.{Item.identifier}.{Mode}.name");
            Item.contentWindow.SetTitle(Item.GetDisplayName(preferCustomName: true));
            Item.Validate();
        }

        private void Toggle()
        {
            var settings = BusConfigurationStore.GetGlobal(Item);
            settings.Mode = settings.Mode switch
            {
                BusMode.External => BusMode.Internal,
                BusMode.Internal => BusMode.Disabled,
                _ => BusMode.External
            };
            UpdateAppearance();
            Refresh();
        }

        private void Connect(GameInventory inventory)
        {
            if (!DigitalLogistics.Instance.Enabled || SaveManager.IsDecoding) return;
            Disconnect();
            Parent = inventory;
            BusController.Attach(this);
            Parent.onSlotAddItemEndFunc += OnInventoryChanged;
            Parent.onSlotRemoveItemEndFunc += OnInventoryChanged;
            Refresh();
        }

        internal void Disconnect()
        {
            if (Parent != null)
            {
                Parent.onSlotAddItemEndFunc -= OnInventoryChanged;
                Parent.onSlotRemoveItemEndFunc -= OnInventoryChanged;
                ClearLinks();
                Parent = null;
                BusController.Detach(this);
            }
            RefreshWindowState();
            BusUI.RefreshConnections(Item);
        }

        internal void Refresh()
        {
            if (SaveManager.IsDecoding) return;
            RefreshWindowState();
            UpdateLinks(GetTargets().ToArray());
            BusUI.RefreshConnections(Item);
        }

        private void RefreshWindowState()
        {
            if (Disabled) Item.contentWindow.Lock();
            else if (Item.contentWindow.isLocked) Item.contentWindow.Unlock();
        }

        private IEnumerable<GameItem> GetTargets()
        {
            if (Disabled || Parent == null || BusAccess.IsBlocked(Item)) yield break;
            if (IsInternal)
            {
                var owner = GraphUtils.FindParentType<GameItem>(Parent);
                if (owner != null && !BusAccess.IsBlocked(owner)) yield return owner;
                yield break;
            }
            if (!(Parent is GameGridInventory) && !(Parent is GameGridScrollableInventory)) yield break;
            var shape = Item.modifiedShape;
            foreach (var target in Parent.childItems)
            {
                if (ReferenceEquals(target, Item) || BusAccess.IsBlocked(target) || !CanTargetExternal(target)) continue;
                for (int column = 0; column < shape.width; column++)
                {
                    var (x, y) = shape.ForwardTransform(column, -1);
                    if (!target.modifiedShape.Intersects(x, y)) continue;
                    yield return target;
                    break;
                }
            }
        }

        private void OnInventoryChanged(GameItem item, GameInventory inventory, SlotMarker slot) => Refresh();
        protected virtual bool CanTargetExternal(GameItem target) => target.contentWindow != null;
        protected virtual void OnMoved() { }
        protected abstract void UpdateLinks(IEnumerable<GameItem> targets);
        protected abstract void ClearLinks();
    }
}
