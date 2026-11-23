using System;
using System.Linq;
using DigitalLogistics.Hoppers;
using DigitalLogistics.Storage;
using DigitalLogistics.Storage.Items;
using DigitalLogistics.Storage.Providers;
using DigitalLogistics.UI;

namespace DigitalLogistics.Item
{
    internal sealed class Hopper : GameItemElement
    {
        internal const string ItemId = "item_hopper";
        internal const string FluidId = "fluid_hopper";
        internal bool IsFluid => identifier == FluidId;
        internal bool Enabled => !Configuration.Disabled;
        internal HopperConfiguration Configuration { get; private set; } = new();
        internal HopperPort Input { get; } = new();
        internal HopperPort Output { get; } = new();

        private readonly HopperUI ui;
        private GameInventory parent;
        private GameInventory watchedInput;
        private InventoryStorageProvider itemSource;
        private InventoryStorageProvider itemTarget;
        private FluidEndpoint fluidSource;
        private FluidEndpoint fluidTarget;
        private bool restoreSelection;
        private bool transferring;

        private bool CanTransfer => HopperSession.CanRun && !transferring && parent != null
            && !Configuration.Disabled && Input.Available && Output.Available && !ItemAccess.IsBlocked(this)
            && (Input.Target != Output.Target || Input.Inventory != Output.Inventory);

        private Hopper(string id) : base(id, "Items/itematlas", "unknown")
        {
            SetSpriteAndShapeFromMod(DigitalLogistics.Instance.Manifest.ID, id)
                .SetShape(new GridShapeBuilder().SetDataFill(2, 1));
            SetGameItemType("MACHINE");
            EnableTag(GeneralHelper.IS_UNLOAD_DISABLED_TAG);
            EnableTag(GeneralHelper.CONFIGURE_TAG);
            shortDescription = DigitalLogistics.Localize($"item.{id}.desc");
            flavorText = DigitalLogistics.Localize($"item.{id}.flavor");
            ui = new HopperUI(this);
            SetContentWindow(ui.Window);
            mayToggleSlotItemFunc = (_, _, _) => true;
            canToggleSlotItemFunc = (_, _, _) => true;
            onToggleSlotItemFunc += (_, _, _) => Toggle();
            mayActivateSlotItemFunc = (_, _, _) => true;
            canActivateSlotItemFunc = (_, _, _) => CanTransfer;
            onActivateSlotItemFunc = (_, _, _) => TryTransfer();
            onConfigureSlotItemFunc = (_, _, _) =>
            {
                RefreshLinks();
                ui.Refresh();
                contentWindow.Show();
            };
            onSlotAddItemFunc += (_, inventory, _) => Connect(inventory);
            onSlotRemoveItemFunc += (_, _, _) => Disconnect();
            onLoaded += _ =>
            {
                Configuration = HopperConfiguration.Load(this);
                restoreSelection = true;
                UpdateAppearance();
                if (parentInventory != null) Connect(parentInventory);
            };
            UpdateAppearance();
        }

        internal static void Register(ModItemDirectory directory)
        {
            directory.Add(ItemId, () => new Hopper(ItemId));
            directory.Add(FluidId, () => new Hopper(FluidId));
        }

        private void UpdateAppearance()
        {
            string suffix = Configuration.Disabled ? "_disabled" : "";
            SetSprite(DigitalLogistics.Instance.Manifest.ID, identifier + suffix);
            string state = Configuration.Disabled ? "disabled." : "";
            name = DigitalLogistics.Localize($"item.{identifier}.{state}name");
            contentWindow.SetTitle(GetDisplayName(preferCustomName: true));
            Validate();
        }

        private void Toggle()
        {
            Configuration.Disabled = !Configuration.Disabled;
            UpdateAppearance();
            RefreshLinks(force: true);
        }

        private void Connect(GameInventory inventory)
        {
            if (!DigitalLogistics.Instance.Enabled || SaveManager.IsDecoding) return;
            Disconnect();
            parent = inventory;
            HopperSession.Attach(this);
            parent.onSlotAddItemEndFunc += OnParentChanged;
            parent.onSlotRemoveItemEndFunc += OnParentChanged;
            RefreshLinks();
        }

        internal void Disconnect()
        {
            if (parent == null) return;
            parent.onSlotAddItemEndFunc -= OnParentChanged;
            parent.onSlotRemoveItemEndFunc -= OnParentChanged;
            parent = null;
            HopperSession.Detach(this);
            ClearLinks();
            Input.Update(null, Array.Empty<GameInventory>());
            Output.Update(null, Array.Empty<GameInventory>());
            ui.Refresh();
        }

        private void OnParentChanged(GameItem _, GameInventory __, SlotMarker ___) => RefreshLinks();

        internal void RefreshLinks(bool force = false)
        {
            if (HopperSession.Loading || SaveManager.IsDecoding) return;
            var (input, output) = FindTargets();
            bool changed = Input.Update(input, GetSlots(input));
            changed |= Output.Update(output, GetSlots(output));
            if (restoreSelection)
            {
                Input.Select(Configuration.InputIndex);
                Output.Select(Configuration.OutputIndex);
                restoreSelection = false;
                changed = true;
            }
            if (!changed && !force) return;
            Rebind();
            ui.Refresh();
        }

        private (GameItem Input, GameItem Output) FindTargets()
        {
            if (!(parent is GameGridInventory || parent is GameGridScrollableInventory)) return (null, null);
            var (inputX, inputY) = modifiedShape.ForwardTransform(-1, 0);
            var (outputX, outputY) = modifiedShape.ForwardTransform(modifiedShape.width, 0);
            GameItem input = null;
            GameItem output = null;
            foreach (var target in parent.childItems)
            {
                if (target == this || target is StorageDisk or Hopper) continue;
                if (input == null && target.modifiedShape.Intersects(inputX, inputY)) input = target;
                if (output == null && target.modifiedShape.Intersects(outputX, outputY)) output = target;
                if (input != null && output != null) break;
            }
            return (input, output);
        }

        private GameInventory[] GetSlots(GameItem target)
        {
            if (IsFluid && FluidEndpoint.IsContainer(target)) return new GameInventory[] { null };
            return GraphUtils.FindChildrenType<GameInventory>(target?.contentWindow,
                inventory => InventoryStorageProvider.GetPanel(inventory)?.role != "note").ToArray();
        }

        internal void SelectSlot(HopperPort port, int index)
        {
            if (index == port.Index) return;
            port.Select(index);
            Rebind();
        }

        private void Rebind()
        {
            HopperSession.Cancel(this);
            ClearLinks();
            if (Configuration.Disabled || Input.Index < 0 || Output.Index < 0) return;
            if (IsFluid)
            {
                fluidSource = new FluidEndpoint(Input.Target, Input.Inventory);
                fluidSource.Changed += OnInputChanged;
                fluidTarget = new FluidEndpoint(Output.Target, Output.Inventory);
            }
            else
            {
                watchedInput = Input.Inventory;
                itemSource = new InventoryStorageProvider(watchedInput);
                itemTarget = new InventoryStorageProvider(Output.Inventory);
                watchedInput.onSlotAddItemEndFunc += OnInputItemChanged;
                watchedInput.onSlotRemoveItemEndFunc += OnInputItemChanged;
                watchedInput.onSlotChangeAmountItemFunc += OnInputItemChanged;
            }
        }

        private void ClearLinks()
        {
            if (watchedInput != null)
            {
                watchedInput.onSlotAddItemEndFunc -= OnInputItemChanged;
                watchedInput.onSlotRemoveItemEndFunc -= OnInputItemChanged;
                watchedInput.onSlotChangeAmountItemFunc -= OnInputItemChanged;
                watchedInput = null;
                itemSource.Dispose();
                itemTarget.Dispose();
                itemSource = itemTarget = null;
            }
            if (fluidSource != null)
            {
                fluidSource.Changed -= OnInputChanged;
                fluidSource.Dispose();
                fluidTarget.Dispose();
                fluidSource = fluidTarget = null;
            }
        }

        private void OnInputItemChanged(GameItem item, GameInventory _, SlotMarker __)
        {
            if (item is not Hopper && !Bus.IsBus(item)) OnInputChanged();
        }

        private void OnInputChanged()
        {
            if (CanTransfer) HopperSession.Schedule(this);
        }

        internal void TryTransfer()
        {
            RefreshLinks();
            if (!CanTransfer) return;
            HopperSession.Cancel(this);
            transferring = true;
            try
            {
                if (IsFluid) TransferFluid();
                else TransferItems();
            }
            finally { transferring = false; }
        }

        private void TransferItems()
        {
            int remaining = DigitalLogistics.Instance.Settings.MaxContinuousItemTransfers.Value;
            var expression = Configuration.GetExpression();
            var inventory = watchedInput;
            var target = itemTarget;
            var source = itemSource;
            foreach (var item in inventory.childItems.ToArray())
            {
                if (remaining <= 0 || source.Disposed || target.Disposed) return;
                if (item.parentInventory != inventory || item is Hopper || Bus.IsBus(item)
                    || ItemAccess.IsPinnedOrBlocked(item)) continue;
                var stored = new StoredPhysicalItem(item, source);
                if (expression.Evaluate(stored))
                    remaining -= StorageTransfer.TryInput(stored, target, remaining).Amount;
            }
        }

        private void TransferFluid()
        {
            fluidSource.Refresh();
            fluidTarget.Refresh();
            int remaining = DigitalLogistics.Instance.Settings.MaxContinuousFluidVolumeUL.Value;
            var expression = Configuration.GetExpression();
            var targets = fluidTarget.Providers.ToArray();
            foreach (var source in fluidSource.Providers.ToArray())
                foreach (var target in targets)
                {
                    if (remaining <= 0) return;
                    if (source.Available && expression.Evaluate(source.Content))
                        remaining -= FluidTransfer.Transfer(source, target, remaining, applyTargetFilter: false).RemovedUL;
                }
        }
    }
}
