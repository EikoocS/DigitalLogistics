using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using DigitalStorage.Filtering;
using DigitalStorage.Storage;
using DigitalStorage.UI;
using UnityEngine;

namespace DigitalStorage.Controller
{
    internal sealed class StorageTerminalInteraction : EmptyActionHandler
    {
        private StorageTerminalController owner;
        private readonly StorageTerminalDragUI view = new();
        private readonly HashSet<int> selection = new();
        private readonly HashSet<int> placedItems = new();
        private StorageTerminalRowController pressedRow;
        private GameItem externalItem;
        private GameItem previewItem;
        private GridShapeBuilder shape;
        private CancellationTokenSource movement;
        private Vector3 pressPosition;
        private bool pointerDown;
        private bool fromList;
        private bool dragged;
        private bool quickTransfer;
        private int inputAmount;
        private int consumeUntilFrame = -1;

        internal bool Consuming => previewItem != null || quickTransfer || movement != null
            || Time.frameCount <= consumeUntilFrame;

        public override List<KeyCode> keyListeners => new() { KeyCode.Mouse0, KeyCode.Escape };

        internal void Init(StorageTerminalController controller) => owner = controller;

        protected override bool OnEventInit()
        {
            if (InputActionManager.current == null || ItemMouseDragHandler.current == null) return false;
            priority = InputActionManager.current.actionHandlers.Where(handler => handler != this)
                .Select(handler => handler.priority).DefaultIfEmpty().Max() + 1;
            return true;
        }

        protected override void OnEventPress(ISet<KeyCode> keysPressed)
        {
            if (keysPressed.Contains(KeyCode.Escape) && (Consuming || pointerDown))
            {
                Cancel();
                return;
            }
            if (!keysPressed.Contains(KeyCode.Mouse0) || movement != null) return;
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
                    externalItem = view.FindItem(Input.mousePosition);
                    if (!CanTakeExternal(externalItem))
                    {
                        quickTransfer = false;
                        externalItem = null;
                        return;
                    }
                    inputAmount = Input.GetKey(KeyCode.LeftAlt) ? 1 : externalItem.MaxNumRemove();
                }
                if (Consuming) CaptureInput();
            }
            pointerDown = true;
            dragged = false;
            pressPosition = Input.mousePosition;
        }

        protected override void OnEventMouseMove(Vector3 mousePosition, Vector3 mouseDelta)
        {
            if (!pointerDown || dragged || (mousePosition - pressPosition).magnitude
                < ItemMouseDragHandler.current.dragThreshold) return;
            dragged = true;
            if (quickTransfer) return;
            if (previewItem == null)
            {
                bool held = pressedRow != null ? HoldSelection() : Hold(externalItem);
                if (!held)
                {
                    Cancel();
                    return;
                }
                CaptureInput();
            }
        }

        protected override void OnEventRelease(ISet<KeyCode> keysReleased)
        {
            if (!keysReleased.Contains(KeyCode.Mouse0) || !pointerDown) return;
            OnEventMouseMove(Input.mousePosition, Vector3.zero);
            pointerDown = false;
            if (quickTransfer)
            {
                quickTransfer = false;
                if (!dragged)
                {
                    if (pressedRow != null && view.FindRow(Input.mousePosition) == pressedRow)
                        TransferToCounter();
                    else if (externalItem != null && view.FindItem(Input.mousePosition) == externalItem)
                        InputExternal();
                }
                consumeUntilFrame = Time.frameCount + 1;
            }
            else if (previewItem != null && (!fromList || dragged))
            {
                if (externalItem != null) PlaceExternal();
                else PlaceOutput(!fromList && dragged);
                consumeUntilFrame = Time.frameCount + 1;
            }
            pressedRow = null;
            if (previewItem == null) externalItem = null;
        }

        protected override bool OnEventUpdate()
        {
            if (previewItem != null && movement == null)
            {
                if (externalItem != null ? !CanTakeExternal(externalItem)
                    : !owner.GetCurrentItems(selection).Any()) Cancel();
            }
            if (!Consuming)
            {
                view.HideInputShield();
                return false;
            }
            view.Update();
            if (previewItem != null && movement == null)
                view.Highlight(view.IsOverTerminal(owner, Input.mousePosition) ? null
                    : CaptureDestination(externalItem == null ? 1 : inputAmount)(previewItem));
            return true;
        }

        private static bool CanTakeExternal(GameItem item) => item is GameItemElement
            && !item.isDebugMenu && item.unitCount > 0 && item.MayRemove() && item.MaxNumRemove() > 0;

        private static bool CanOutput(GameItem item) => item is GameItemElement && item.unitCount > 0
            && (item.parentInventory == null || item.MayRemove()) && item.MaxNumRemove() > 0;

        private void SetSelection(StorageTerminalRowController row)
        {
            selection.Clear();
            placedItems.Clear();
            foreach (var item in row.Group?.Items ?? new List<StoredItem> { row.Item })
                selection.Add(item.UniqueId);
        }

        private bool HoldSelection()
        {
            foreach (var stored in owner.GetCurrentItems(selection))
            {
                if (placedItems.Contains(stored.UniqueId)) continue;
                if (Hold(stored.GetItemInstance())) return true;
            }
            return false;
        }

        private bool Hold(GameItem item)
        {
            if (!CanOutput(item)) return false;
            previewItem = item;
            shape = new GridShapeBuilder(item.modifiedShape);
            view.ShowPreview(item);
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

        private void PlaceOutput(bool continueHolding)
        {
            if (view.IsOverTerminal(owner, Input.mousePosition)) return;
            MoveOutput(CaptureDestination(1), continueHolding);
        }

        private async void MoveOutput(Func<GameItem, SlotMarker> findSlot, bool continueHolding)
        {
            var items = owner.GetCurrentItems(selection)
                .Where(item => !placedItems.Contains(item.UniqueId)).ToList();
            var ids = new HashSet<int>(items.Select(item => item.UniqueId));
            if (ids.Count == 0) return;
            var expression = new StoredItemExpression(Expression.Call(Expression.Constant(ids),
                nameof(HashSet<int>.Contains), null,
                Expression.Property(FilterExpressionBuilder.Item, nameof(StoredItem.UniqueId))))
                .And(StoredItemExpression.DisplayName.EqualTo(items[0].DisplayName));
            var cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            movement = cancellation;
            view.ClearHighlight();
            try
            {
                bool moved = await TryMoveOutput(expression, findSlot, token);
                if (!moved || token.IsCancellationRequested) return;
                if (!continueHolding || !HoldSelection()) ClearHeld();
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
                if (ReferenceEquals(movement, cancellation)) movement = null;
                cancellation.Dispose();
            }
        }

        private async Task<bool> TryMoveOutput(StoredItemExpression expression,
            Func<GameItem, SlotMarker> findSlot, CancellationToken token)
        {
            foreach (var provider in DigitalStorage.Manager.Providers.ToArray())
            {
                await foreach (var output in provider.PrepareOutputAsync(expression,
                    owner.Configuration.SortBy, owner.Configuration.Reverse, token))
                {
                    token.ThrowIfCancellationRequested();
                    if (!CanOutput(output.Item)) continue;
                    int uniqueId = output.Item.GetUniqueID();
                    var slot = findSlot(output.Item);
                    int moved = slot?.TryAcceptOnce(1) ?? 0;
                    output.Complete(moved);
                    if (moved == 0) continue;
                    owner.RemoveItem(uniqueId);
                    placedItems.Add((slot.targetItem ?? slot.item).GetUniqueID());
                    return true;
                }
            }
            return false;
        }

        private void InputExternal()
        {
            if (!CanTakeExternal(externalItem)) return;
            int uniqueId = externalItem.GetUniqueID();
            var result = DigitalStorage.Manager.TryInput(externalItem, inputAmount);
            if (result.Amount == 0) return;
            if (!result.Items.Any(item => item.UniqueId == uniqueId)) owner.RemoveItem(uniqueId);
            foreach (var item in result.Items) owner.InsertItem(item);
        }

        private void PlaceExternal()
        {
            if (!CanTakeExternal(externalItem)) return;
            if (view.IsOverTerminal(owner, Input.mousePosition)) InputExternal();
            else
            {
                using var output = new GameItemStoredItem(externalItem).PrepareOutput();
                int uniqueId = output.Item.GetUniqueID();
                var slot = CaptureDestination(inputAmount)(output.Item);
                int moved = slot?.TryAcceptOnce(inputAmount) ?? 0;
                output.Complete(moved);
                if (moved > 0) owner.RemoveItem(uniqueId);
            }
            ClearHeld();
        }

        private void ClearHeld()
        {
            view.ClearHighlight();
            view.HidePreview();
            previewItem = null;
            externalItem = null;
            pressedRow = null;
            selection.Clear();
            placedItems.Clear();
            pointerDown = false;
            quickTransfer = false;
        }

        private void Cancel()
        {
            movement?.Cancel();
            ClearHeld();
            consumeUntilFrame = Time.frameCount + 1;
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
