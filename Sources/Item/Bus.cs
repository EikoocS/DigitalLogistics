using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Controller;
using DigitalLogistics.UI;
using UnityEngine;

namespace DigitalLogistics.Item
{
    internal abstract class Bus
    {
        internal const string InternalTag = "DIGITAL_LOGISTICS_BUS_INTERNAL_TAG";
        internal const string DisabledTag = "DIGITAL_LOGISTICS_BUS_DISABLED_TAG";

        internal GameItem Item { get; }
        protected GameInventory Parent { get; private set; }
        protected bool IsInternal => Item.IsTag(InternalTag);
        internal bool Disabled => Item.IsTag(DisabledTag);
        internal bool CanExecute => !Disabled && HasLinks && GeneralHelper.IsItemOwned(Item);
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
            Item.shortDescription = StorageTerminal.Localize($"item.{id}.desc");
            Item.flavorText = StorageTerminal.Localize($"item.{id}.flavor");
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
                new(((GameItemElement)self).ResolveSpriteByName($"{self.identifier}_{Mode}"), Vector2.zero)
            };
            UpdateAppearance();
        }

        internal static bool IsBus(GameItem item) => item?.identifier is StorageBus.ItemId
            or IOBus.InputId or IOBus.OutputId or IOBus.InputOutputId or TriggerBus.ItemId;

        internal static void SuppressClickActivation(GameItem item, ref bool activate)
        {
            if (IsBus(item)) activate = false;
        }

        private string Mode => Disabled ? "disabled" : IsInternal ? "internal" : "external";

        private void UpdateAppearance()
        {
            Item.SetSprite(DigitalLogistics.Instance.Manifest.ID, Item.identifier);
            Item.name = StorageTerminal.Localize($"item.{Item.identifier}.{Mode}.name");
            Item.contentWindow.SetTitle(Item.GetDisplayName(preferCustomName: true));
            Item.Validate();
        }

        private void Toggle()
        {
            if (Disabled) Item.DisableTag(DisabledTag);
            else if (IsInternal)
            {
                Item.DisableTag(InternalTag);
                Item.EnableTag(DisabledTag);
            }
            else Item.EnableTag(InternalTag);
            UpdateAppearance();
            Refresh();
        }

        private void Connect(GameInventory inventory)
        {
            if (!DigitalLogistics.Instance.Enabled) return;
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
            UpdateLinks(GetTargets().ToArray());
            RefreshWindowState();
            BusUI.RefreshConnections(Item);
        }

        private void RefreshWindowState()
        {
            if (Disabled) Item.contentWindow.Lock();
            else if (Item.contentWindow.isLocked) Item.contentWindow.Unlock();
        }

        private IEnumerable<GameItem> GetTargets()
        {
            if (Disabled || Parent == null) yield break;
            if (IsInternal)
            {
                var owner = GraphUtils.FindParentType<GameItem>(Parent);
                if (owner != null) yield return owner;
                yield break;
            }
            if (!(Parent is GameGridInventory) && !(Parent is GameGridScrollableInventory)) yield break;
            var shape = Item.modifiedShape;
            foreach (var target in Parent.childItems)
            {
                if (ReferenceEquals(target, Item) || target.contentWindow == null) continue;
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
        protected virtual void OnMoved() { }
        protected abstract void UpdateLinks(IEnumerable<GameItem> targets);
        protected abstract void ClearLinks();
    }
}
