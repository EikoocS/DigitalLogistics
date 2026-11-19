using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DigitalLogistics.Filtering;
using DigitalLogistics.Storage;
using DigitalLogistics.Storage.Items;
using DigitalLogistics.UI;
using UnityEngine;

namespace DigitalLogistics.Controller
{
    internal sealed class StorageTerminalInteraction : EmptyActionHandler
    {
        private static int escapeConsumedFrame = -1;
        private StorageTerminalController owner;
        private readonly StorageTerminalDragUI view = new();
        private readonly HashSet<int> selection = new();
        private readonly HashSet<int> groupSelection = new();
        private readonly HashSet<int> placedItems = new();
        private readonly HashSet<(GameInventory, int, int)> paintedSlots = new();
        private StorageTerminalRowController pressedRow;
        private GameItem quickInputItem;
        private ItemInstanceLease previewLease;
        private GameItem previewItem => previewLease?.Item;
        private GridShapeBuilder shape;
        private CancellationTokenSource movement;
        private Vector3 pressPosition;
        private bool pointerDown;
        private bool fromList;
        private bool dragged;
        private bool quickTransfer;
        private bool pressedGroup;
        private int inputAmount;
        private int consumeUntilFrame = -1;

        internal bool Consuming => previewItem != null || quickTransfer || movement != null
            || Time.frameCount <= consumeUntilFrame;
        internal bool HasActiveInput => pointerDown || Consuming;

        public override List<KeyCode> keyListeners => new()
        {
            KeyCode.Mouse0, KeyCode.Escape,
            KeyBindHelper.Get("rotate_cw"), KeyBindHelper.Get("rotate_ccw"),
            KeyBindHelper.Get("flip_item"), KeyBindHelper.Get("flip_item_alt"),
            KeyBindHelper.Get("reset_orientation"), KeyBindHelper.Get("secondary_click")
        };

        internal void Init(StorageTerminalController controller) => owner = controller;

        internal static bool TryCancelHeldItem()
        {
            if (escapeConsumedFrame == Time.frameCount) return true;
            if (InputActionManager.current == null) return false;
            foreach (var handler in InputActionManager.current.actionHandlers.OfType<StorageTerminalInteraction>())
            {
                if (!handler.isActiveAndEnabled || handler.previewItem == null && handler.movement == null) continue;
                handler.Cancel();
                escapeConsumedFrame = Time.frameCount;
                return true;
            }
            return false;
        }

        protected override bool OnEventInit()
        {
            if (InputActionManager.current == null || ItemMouseDragHandler.current == null) return false;
            priority = InputActionManager.current.actionHandlers.Where(handler => handler != this)
                .Select(handler => handler.priority).DefaultIfEmpty().Max() + 1;
            return true;
        }

        protected override void OnEventPress(ISet<KeyCode> keysPressed)
        {
            if (!isActiveAndEnabled || owner.Refreshing) return;
            if (keysPressed.Contains(KeyCode.Escape) && TryCancelHeldItem()) return;
            if (previewItem != null && movement == null)
            {
                var drag = ItemMouseDragHandler.current;
                bool swap = EmporiumOption.Instance != null && EmporiumOption.Instance.swapDragRotateFlip;
                bool clockwise = keysPressed.Contains(drag.rotateCWKey) || swap && keysPressed.Contains(drag.secondaryKey);
                bool counterclockwise = keysPressed.Contains(drag.rotateCCWKey);
                bool flip = keysPressed.Contains(drag.flipKey) || keysPressed.Contains(drag.flipAltKey)
                    || !swap && keysPressed.Contains(drag.secondaryKey);
                bool reset = keysPressed.Contains(drag.resetKey);
                if (clockwise || counterclockwise || flip || reset)
                    ChangeOrientation((counterclockwise ? 1 : 0) - (clockwise ? 1 : 0), flip, reset);
            }
            if (!keysPressed.Contains(KeyCode.Mouse0) || movement != null) return;
            if (previewItem != null && view.IsOverTerminalWindow(Input.mousePosition))
            {
                Cancel();
                return;
            }
            if (!Consuming) view.HideInputShield();

            fromList = previewItem == null;
            if (fromList)
            {
                if (ItemMouseDragHandler.current.IsDraggingItem || UIDragHandler.isDraggingAnyUI) return;
                pressedRow = view.FindRow(Input.mousePosition);
                if (pressedRow != null && pressedRow.Owner != owner) return;
                quickTransfer = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (pressedRow != null)
                {
                    SetSelection(pressedRow);
                    if (!quickTransfer && pressedRow.Group == null && !HoldSelection()) return;
                }
                else
                {
                    bool quickInput = quickTransfer;
                    quickTransfer = false;
                    if (!quickInput) return;
                    quickInputItem = view.FindItem(Input.mousePosition);
                    if (!CanTakeExternal(quickInputItem))
                    {
                        quickInputItem = null;
                        return;
                    }
                    inputAmount = Input.GetKey(KeyCode.LeftAlt) ? 1 : quickInputItem.MaxNumRemove();
                }
                if (Consuming) CaptureInput();
            }
            pointerDown = true;
            dragged = false;
            paintedSlots.Clear();
            pressPosition = Input.mousePosition;
        }

        protected override void OnEventMouseMove(Vector3 mousePosition, Vector3 mouseDelta)
        {
            if (!isActiveAndEnabled || owner.Refreshing) return;
            if (!pointerDown || dragged || (mousePosition - pressPosition).magnitude
                < ItemMouseDragHandler.current.dragThreshold) return;
            dragged = true;
            if (quickInputItem != null)
            {
                quickInputItem = null;
                pointerDown = false;
                return;
            }
            if (quickTransfer) return;
            if (previewItem == null)
            {
                if (!HoldSelection())
                {
                    Cancel();
                    return;
                }
                CaptureInput();
            }
        }

        protected override void OnEventMouseScroll(Vector2 scrollDelta)
        {
            if (!isActiveAndEnabled || owner.Refreshing || previewItem == null || movement != null
                || Input.GetKey(ItemMouseDragHandler.current.scrollIgnoreKey)) return;
            int direction = (int)(scrollDelta.x + scrollDelta.y);
            if (direction == 0) return;
            bool swap = EmporiumOption.Instance != null && EmporiumOption.Instance.swapDragRotateFlip;
            ChangeOrientation(swap ? 0 : direction > 0 ? 1 : -1, swap, false);
        }

        private void ChangeOrientation(int rotation, bool flip, bool reset)
        {
            shape.SetOrientation(shape.orientation + rotation);
            if (flip)
            {
                if (shape.orientation % 2 != 0) shape.SetOrientation(shape.orientation + 2);
                shape.SetFlipped(!shape.flipped);
            }
            if (reset) shape.SetOrientation(0).SetFlipped(false);
            view.SetShape(shape);
        }

        protected override void OnEventRelease(ISet<KeyCode> keysReleased)
        {
            if (!isActiveAndEnabled || owner.Refreshing) return;
            if (!keysReleased.Contains(KeyCode.Mouse0) || !pointerDown) return;
            OnEventMouseMove(Input.mousePosition, Vector3.zero);
            pointerDown = false;
            if (quickInputItem != null)
            {
                if (view.FindItem(Input.mousePosition) == quickInputItem)
                {
                    CaptureInput();
                    InputExternal(quickInputItem, inputAmount);
                    consumeUntilFrame = Time.frameCount + 1;
                }
                quickInputItem = null;
                return;
            }
            if (quickTransfer)
            {
                quickTransfer = false;
                if (!dragged)
                {
                    if (pressedRow != null && view.FindRow(Input.mousePosition) == pressedRow)
                        TransferToCounter();
                }
                consumeUntilFrame = Time.frameCount + 1;
            }
            else if (previewItem != null && (!fromList || dragged))
            {
                PlaceOutput(!fromList && dragged, fromList && dragged, !fromList && dragged);
                consumeUntilFrame = Time.frameCount + 1;
            }
            pressedRow = null;
        }

        protected override bool OnEventUpdate()
        {
            if (!isActiveAndEnabled || owner.Refreshing) return false;
            if (previewItem != null && movement == null)
            {
                if (!owner.GetCurrentItems(selection).Any()) Cancel();
            }
            if (!Consuming)
            {
                view.HideInputShield();
                return false;
            }
            view.Update();
            if (pointerDown && !fromList && dragged && previewItem != null && movement == null)
                PlaceOutput(true, false, true);
            if (previewItem != null && movement == null)
                view.Highlight(view.IsOverTerminal(owner, Input.mousePosition) ? null
                    : CaptureDestination(1)(previewItem));
            return true;
        }

        private static bool CanTakeExternal(GameItem item) => item is GameItemElement element && !element.IsDestroyed()
            && !ItemPinHelper.IsPinned(item) && !item.isDebugMenu && item.unitCount > 0 && item.MayRemove() && item.MaxNumRemove() > 0;

        private static bool CanOutput(GameItem item) => item is GameItemElement && item.unitCount > 0
            && (item.parentInventory == null || item.MayRemove()) && item.MaxNumRemove() > 0;

        private void SetSelection(StorageTerminalRowController row)
        {
            selection.Clear();
            groupSelection.Clear();
            placedItems.Clear();
            pressedGroup = quickTransfer && row.Group != null;
            foreach (var item in row.Group?.Items ?? new List<CachedItem> { row.Item })
                selection.Add(item.UniqueId);
            foreach (var item in owner.GetGroupItems(row.Group?.Name ?? row.Item.DisplayName))
                groupSelection.Add(item.UniqueId);
        }

        private bool HoldSelection()
        {
            foreach (var stored in owner.GetCurrentItems(selection))
            {
                if (placedItems.Contains(stored.UniqueId)) continue;
                if (Hold(stored)) return true;
            }
            return false;
        }

        private bool Hold(CachedItem stored)
        {
            var lease = stored.AcquireInstance();
            if (lease == null || !CanOutput(lease.Item))
            {
                lease?.Dispose();
                return false;
            }
            var nextShape = new GridShapeBuilder(lease.Item.modifiedShape);
            if (previewItem != null) nextShape.SetOrientation(shape.orientation).SetFlipped(shape.flipped);
            view.HidePreview();
            previewLease?.Dispose();
            previewLease = lease;
            shape = nextShape;
            view.ShowPreview(lease.Item, shape);
            return true;
        }

        private void CaptureInput()
        {
            InputActionManager.current.ResetAfter(this);
            view.ShowInputShield();
        }

        private Func<GameItem, SlotMarker> CaptureDestination(int amount)
        {
            var position = Input.mousePosition;
            var target = view.FindItem(position);
            var inventory = target == null ? view.FindInventory(position) : null;
            var placementShape = new GridShapeBuilder(shape);
            return item =>
            {
                if (target != null)
                {
                    if (target == item || !GeneralHelper.MayPlayerInsertInto(target)) return null;
                    if (target.MayHaveValidInventorySlot(item)) return target.TryFindOneValidInventorySlot(item);
                    return target.MayStack(item) ? target.GetStackSlot(item) : null;
                }
                if (inventory == null || !GeneralHelper.MayPlayerInsertInto(inventory)) return null;
                var itemShape = new GridShapeBuilder(item.modifiedShape)
                    .SetOrientation(placementShape.orientation).SetFlipped(placementShape.flipped);
                return inventory.TryInventorySlot(item, amount, position, itemShape, item.modifiedState);
            };
        }

        private void TransferToCounter()
        {
            var entry = EmporiumEntry.Instance;
            var counter = entry.backInvinvElement;
            if (!entry.backInvWindow.IsVisible() || !GeneralHelper.MayPlayerInsertInto(counter)) return;
            MoveOutput(item => item.parentInventory == counter ? null
                : counter.TryFindOneValidInventorySlot(item), false);
        }

        private static (GameInventory, int, int) GetSlotPosition(SlotMarker slot)
        {
            var gridShape = slot.itemGridShape ?? slot.targetItem.modifiedShape;
            return (slot.inventory ?? slot.targetItem.parentInventory, gridShape.minX, gridShape.minY);
        }

        private void PlaceOutput(bool continueHolding, bool releaseAfterAttempt, bool continuous = false)
        {
            if (movement != null) return;
            if (view.IsOverTerminal(owner, Input.mousePosition))
            {
                if (releaseAfterAttempt) ClearHeld();
                return;
            }
            var findSlot = CaptureDestination(1);
            if (continuous)
            {
                var previewSlot = findSlot(previewItem);
                if (previewSlot == null || previewSlot.numTransfer <= 0
                    || paintedSlots.Contains(GetSlotPosition(previewSlot))) return;
                MoveOutput(item =>
                {
                    var slot = findSlot(item);
                    return slot?.numTransfer > 0 && paintedSlots.Add(GetSlotPosition(slot)) ? slot : null;
                }, true, continueGroup: true);
            }
            else MoveOutput(findSlot, continueHolding, releaseAfterAttempt);
        }

        private async void MoveOutput(Func<GameItem, SlotMarker> findSlot, bool continueHolding,
            bool releaseAfterAttempt = false, bool continueGroup = false)
        {
            var items = owner.GetCurrentItems(selection)
                .Where(item => !placedItems.Contains(item.UniqueId)).ToList();
            var ids = new HashSet<int>(items.Select(item => item.UniqueId));
            if (ids.Count == 0)
            {
                if (releaseAfterAttempt) ClearHeld();
                return;
            }
            var expression = CachedItemExpression.HasIds(ids)
                .And(CachedItemExpression.DisplayName.EqualTo(items[0].DisplayName));
            var cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            movement = cancellation;
            view.ClearHighlight();
            try
            {
                bool moved = await TryMoveOutput(expression, findSlot, token);
                if (!moved || token.IsCancellationRequested) return;
                if (continueGroup) selection.UnionWith(groupSelection);
                if (!releaseAfterAttempt && (!continueHolding || !HoldSelection())) ClearHeld();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                if (releaseAfterAttempt) ClearHeld();
                if (ReferenceEquals(movement, cancellation)) movement = null;
                cancellation.Dispose();
            }
        }

        private async Task<bool> TryMoveOutput(CachedItemExpression expression,
            Func<GameItem, SlotMarker> findSlot, CancellationToken token)
        {
            await foreach (var stored in DigitalLogistics.Manager.SearchAsync(expression,
                owner.Configuration.SortBy, owner.Configuration.Reverse, cancellationToken: token,
                prioritizeOutput: pressedGroup))
            {
                token.ThrowIfCancellationRequested();
                var result = StorageTransfer.TryPlace(stored, findSlot, 1);
                if (result.Amount == 0) continue;
                ApplyTransfer(result);
                placedItems.Add(result.PlacedItemId.Value);
                return true;
            }
            return false;
        }

        internal void InputExternal(GameItem item, int amount)
        {
            if (!CanTakeExternal(item)) return;
            var result = StorageTransfer.TryInput(item, amount);
            if (result.Amount > 0) ApplyTransfer(result);
        }

        private void ApplyTransfer(StorageTransferResult result)
        {
            if (result.RemainingSource == null) owner.RemoveItem(result.SourceId);
            else owner.InsertItem(result.RemainingSource);
            foreach (var item in result.Items) owner.InsertItem(item);
        }

        private void ClearHeld()
        {
            view.ClearHighlight();
            view.HidePreview();
            previewLease?.Dispose();
            previewLease = null;
            shape = null;
            quickInputItem = null;
            pressedRow = null;
            selection.Clear();
            groupSelection.Clear();
            placedItems.Clear();
            paintedSlots.Clear();
            pointerDown = false;
            quickTransfer = false;
        }

        private void Cancel()
        {
            movement?.Cancel();
            ClearHeld();
            consumeUntilFrame = Time.frameCount + 1;
        }

        internal void Suspend()
        {
            Cancel();
            view.HideInputShield();
        }

        protected override void OnEventReset()
        {
            if (Consuming || pointerDown) Cancel();
        }

        private void OnDisable()
        {
            movement?.Cancel();
            ClearHeld();
            view.HideInputShield();
        }

        private void OnDestroy()
        {
            if (InputActionManager.current != null) InputActionManager.current.actionHandlers.Remove(this);
        }
    }
}
