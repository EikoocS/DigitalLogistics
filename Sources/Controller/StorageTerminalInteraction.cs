#if false
using System.Collections.Generic;
using System.Linq;
using DigitalStorage.Storage;
using DigitalStorage.UI;
using UnityEngine;

namespace DigitalStorage.Controller
{
    internal sealed class StorageTerminalInteraction : EmptyActionHandler
    {
        private StorageTerminalController owner;
        private StoredItem held;
        private StorageTerminalRowController quickTransferRow;
        private StoredItem groupDragItem;
        private GridShapeBuilder shape;
        private readonly StorageTerminalDragUI view = new();
        private readonly HashSet<GameItem> placedItems = new();
        private Vector3 pressPosition;
        private bool fromList;
        private bool dragged;
        private int consumeUntilFrame = -1;

        internal bool PointerDown { get; private set; }

        public override List<KeyCode> keyListeners => new() { KeyCode.Mouse0, KeyCode.Escape };

        internal void Init(StorageTerminalController list) => owner = list;

        protected override bool OnEventInit()
        {
            if (InputActionManager.current == null || ItemMouseDragHandler.current == null) return false;
            priority = InputActionManager.current.actionHandlers.Where(h => h != this)
                .Select(h => h.priority).DefaultIfEmpty().Max() + 1;
            return true;
        }

        protected override void OnEventPress(ISet<KeyCode> keysPressed)
        {
            if (keysPressed.Contains(KeyCode.Escape)
                && (held != null || quickTransferRow != null || groupDragItem != null))
            {
                Cancel();
                return;
            }
            if (!keysPressed.Contains(KeyCode.Mouse0)) return;
            if (held == null && Time.frameCount > consumeUntilFrame) view.HideInputShield();

            fromList = held == null;
            if (fromList)
            {
                var row = StorageTerminalRow.FindController(Input.mousePosition);
                if (row?.Owner != owner
                    || ItemMouseDragHandler.current.IsDraggingItem || UIDragHandler.isDraggingAnyUI) return;
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) quickTransferRow = row;
                else if (row.Group != null)
                {
                    var first = row.Group.Items[0];
                    if (!CanTake(first)) return;
                    groupDragItem = first;
                }
                else
                {
                    if (!CanTake(row.Item)) return;
                    Hold(row.Item);
                }
                if (groupDragItem == null)
                {
                    InputActionManager.current.ResetAfter(this);
                    view.ShowInputShield();
                }
            }
            PointerDown = true;
            dragged = false;
            pressPosition = Input.mousePosition;
        }

        protected override void OnEventMouseMove(Vector3 mousePosition, Vector3 mouseDelta)
        {
            if (PointerDown && mousePosition != pressPosition
                && (mousePosition - pressPosition).magnitude >= ItemMouseDragHandler.current.dragThreshold)
            {
                dragged = true;
                if (groupDragItem != null)
                {
                    if (!CanTake(groupDragItem))
                    {
                        Cancel();
                        return;
                    }
                    Hold(groupDragItem);
                    groupDragItem = null;
                    InputActionManager.current.ResetAfter(this);
                    view.ShowInputShield();
                }
            }
        }

        protected override void OnEventRelease(ISet<KeyCode> keysReleased)
        {
            if (!keysReleased.Contains(KeyCode.Mouse0) || !PointerDown) return;
            OnEventMouseMove(Input.mousePosition, Vector3.zero);
            PointerDown = false;
            if (quickTransferRow != null)
            {
                if (!dragged && view.FindRow(Input.mousePosition) == quickTransferRow)
                    TransferOneToCounter(quickTransferRow);
                quickTransferRow = null;
            }
            else if (held != null && (!fromList || dragged)) PlaceOne(!fromList && dragged);
            groupDragItem = null;
            consumeUntilFrame = Time.frameCount + 1;
        }

        protected override bool OnEventUpdate()
        {
            if (held != null && !CanTake(held)) Cancel();
            bool consuming = held != null || PointerDown || Time.frameCount <= consumeUntilFrame;
            if (!consuming)
            {
                view.HideInputShield();
                return false;
            }

            view.Update();
            if (held != null) view.Highlight(FindSlot());
            return true;
        }

        private static bool CanTake(StoredItem item) => item?.IsAvailable == true
            && item.Item is GameItemElement && item.Item.MayRemove() && item.Item.MaxNumRemove() > 0;

        private static void TransferOneToCounter(StorageTerminalRowController row)
        {
            var entry = EmporiumEntry.Instance;
            var counter = entry.backInvinvElement;
            if (!entry.backInvWindow.IsVisible() || !GeneralHelper.MayPlayerInsertInto(counter)) return;
            var items = row.Group?.Items ?? new List<StoredItem> { row.Item };
            foreach (var stored in items)
            {
                if (!CanTake(stored) || stored.Item.parentInventory == counter) continue;
                var slot = counter.TryFindOneValidInventorySlot(stored.Item);
                if (slot?.numTransfer > 0 && slot.TryAcceptOnce(1) > 0) return;
            }
        }

        private void Hold(StoredItem item)
        {
            held = item;
            shape = new GridShapeBuilder(item.Item.modifiedShape);
            view.ShowPreview(item.Item);
        }

        private SlotMarker FindSlot()
        {
            var item = held.Item;
            var target = view.FindItem(Input.mousePosition);
            if (target != null)
            {
                if (target == item || !GeneralHelper.MayPlayerInsertInto(target)) return null;
                if (target.MayHaveValidInventorySlot(item)) return target.TryFindOneValidInventorySlot(item);
                return target.MayStack(item) ? target.GetStackSlot(item) : null;
            }
            var inventory = view.FindInventory(Input.mousePosition);
            if (inventory == null || !GeneralHelper.MayPlayerInsertInto(inventory)) return null;
            return inventory.TryInventorySlot(item, 1, Input.mousePosition, shape, item.modifiedState);
        }

        private void PlaceOne(bool continueHolding)
        {
            if (!CanTake(held))
            {
                Cancel();
                return;
            }
            var slot = FindSlot();
            if (slot == null || slot.numTransfer < 1) return;
            string name = held.DisplayName;
            view.ClearHighlight();
            if (slot.TryAcceptOnce(1) < 1) return;
            placedItems.Add(slot.targetItem ?? slot.item);
            var next = continueHolding ? DigitalStorage.Manager.FindItem(name, placedItems) : null;
            if (next == null) ClearHeld();
            else Hold(next);
        }

        private void ClearHeld()
        {
            view.ClearHighlight();
            view.HidePreview();
            held = null;
            quickTransferRow = null;
            groupDragItem = null;
            placedItems.Clear();
            PointerDown = false;
        }

        private void Cancel()
        {
            ClearHeld();
            consumeUntilFrame = Time.frameCount + 1;
        }

        protected override void OnEventReset()
        {
            if (held != null || quickTransferRow != null || groupDragItem != null) Cancel();
        }

        private void OnDisable()
        {
            enabled = false;
            ClearHeld();
            view.HideInputShield();
        }

        private void OnDestroy()
        {
            if (InputActionManager.current != null) InputActionManager.current.actionHandlers.Remove(this);
        }
    }
}
#endif
