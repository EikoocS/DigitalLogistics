using System.Collections.Generic;
using System.Linq;
using DigitalStorage.Storage;
using UnityEngine;

namespace DigitalStorage
{
    internal sealed class StorageTerminalInteraction : EmptyActionHandler
    {
        private StorageTerminalList owner;
        private StoredItem held;
        private StorageTerminalRow quickTransferRow;
        private GridShapeBuilder shape;
        private StorageItemPreview preview;
        private TreeNodeRender inputShield;
        private readonly HighlightManager highlights = new();
        private readonly HashSet<GameItem> placedItems = new();
        private Vector3 pressPosition;
        private bool fromList;
        private bool dragged;
        private int consumeUntilFrame = -1;

        internal bool PointerDown { get; private set; }

        public override List<KeyCode> keyListeners => new() { KeyCode.Mouse0, KeyCode.Escape };

        internal void Init(StorageTerminalList list) => owner = list;

        protected override bool OnEventInit()
        {
            if (InputActionManager.current == null || ItemMouseDragHandler.current == null) return false;
            priority = InputActionManager.current.actionHandlers.Where(h => h != this)
                .Select(h => h.priority).DefaultIfEmpty().Max() + 1;
            return true;
        }

        protected override void OnEventPress(ISet<KeyCode> keysPressed)
        {
            if (keysPressed.Contains(KeyCode.Escape) && (held != null || quickTransferRow != null))
            {
                Cancel();
                return;
            }
            if (!keysPressed.Contains(KeyCode.Mouse0)) return;
            if (held == null && Time.frameCount > consumeUntilFrame) RemoveInputShield();

            fromList = held == null;
            if (fromList)
            {
                var row = RenderHandler.RaycastElement<StorageTerminalRow>(Input.mousePosition);
                if (row?.Owner != owner
                    || ItemMouseDragHandler.current.IsDraggingItem || UIDragHandler.isDraggingAnyUI) return;
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) quickTransferRow = row;
                else
                {
                    if (!CanTake(row.Item)) return;
                    Hold(row.Item);
                }
                InputActionManager.current.ResetAfter(this);
                CreateInputShield();
            }
            PointerDown = true;
            dragged = false;
            pressPosition = Input.mousePosition;
        }

        protected override void OnEventMouseMove(Vector3 mousePosition, Vector3 mouseDelta)
        {
            if (PointerDown && (mousePosition - pressPosition).magnitude >= ItemMouseDragHandler.current.dragThreshold)
                dragged = true;
        }

        protected override void OnEventRelease(ISet<KeyCode> keysReleased)
        {
            if (!keysReleased.Contains(KeyCode.Mouse0) || !PointerDown) return;
            OnEventMouseMove(Input.mousePosition, Vector3.zero);
            PointerDown = false;
            if (quickTransferRow != null)
            {
                if (!dragged && RenderHandler.RaycastElement<StorageTerminalRow>(Input.mousePosition, inputShield)
                    == quickTransferRow) TransferOneToCounter(quickTransferRow);
                quickTransferRow = null;
            }
            else if (!fromList || dragged) PlaceOne(!fromList && dragged);
            consumeUntilFrame = Time.frameCount + 1;
        }

        protected override bool OnEventUpdate()
        {
            if (held != null && !CanTake(held)) Cancel();
            bool consuming = held != null || PointerDown || Time.frameCount <= consumeUntilFrame;
            if (!consuming)
            {
                RemoveInputShield();
                return false;
            }

            OverlayHandler.current.HideTooltip();
            if (inputShield != null)
            {
                inputShield.rectTransform.position = new Vector3(Screen.width / 2f, Screen.height / 2f);
                var scale = inputShield.rectTransform.lossyScale;
                inputShield.rectTransform.sizeDelta = new Vector2(Screen.width / scale.x, Screen.height / scale.y);
            }
            if (held != null)
            {
                preview.Move(Input.mousePosition);
                var slot = FindSlot();
                highlights.Try(slot, slot?.numTransfer > 0
                    ? RenderHandler.BackgroundColor.Green : RenderHandler.BackgroundColor.Red);
            }
            return true;
        }

        private static bool CanTake(StoredItem item) => item?.IsAvailable == true
            && item.Item is GameItemElement && item.Item.MayRemove() && item.Item.MaxNumRemove() > 0;

        private static void TransferOneToCounter(StorageTerminalRow row)
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
            preview?.Destroy();
            held = item;
            shape = new GridShapeBuilder(item.Item.modifiedShape);
            preview = new StorageItemPreview((GameItemElement)item.Item);
        }

        private SlotMarker FindSlot()
        {
            var item = held.Item;
            var target = RenderHandler.RaycastElement<GameItem>(Input.mousePosition, inputShield);
            if (target != null)
            {
                if (target == item || !GeneralHelper.MayPlayerInsertInto(target)) return null;
                if (target.MayHaveValidInventorySlot(item)) return target.TryFindOneValidInventorySlot(item);
                return target.MayStack(item) ? target.GetStackSlot(item) : null;
            }
            var inventory = RenderHandler.RaycastFindParentElement<GameInventory>(Input.mousePosition, inputShield);
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
            highlights.Clear();
            if (slot.TryAcceptOnce(1) < 1) return;
            placedItems.Add(slot.targetItem ?? slot.item);
            var next = continueHolding ? DigitalStorage.Manager.FindItem(name, placedItems) : null;
            if (next == null) ClearHeld();
            else Hold(next);
        }

        private void CreateInputShield()
        {
            inputShield = TreeNodeRender.Instantiate().SetParentOverlayRoot();
            inputShield.gameObject.name = "StorageTerminalInputShield";
            inputShield.InitEventHandler();
            inputShield.image.enabled = true;
            inputShield.image.alphaHitTestMinimumThreshold = 0;
            inputShield.image.color = Color.clear;
            inputShield.rectTransform.anchorMin = inputShield.rectTransform.anchorMax =
                inputShield.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        private void RemoveInputShield()
        {
            if (inputShield == null) return;
            inputShield.gameObject.SetActive(false);
            Destroy(inputShield.gameObject);
            inputShield = null;
        }

        private void ClearHeld()
        {
            highlights.Clear();
            preview?.Destroy();
            preview = null;
            held = null;
            quickTransferRow = null;
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
            if (held != null || quickTransferRow != null) Cancel();
        }

        private void OnDisable()
        {
            enabled = false;
            ClearHeld();
            RemoveInputShield();
        }

        private void OnDestroy()
        {
            if (InputActionManager.current != null) InputActionManager.current.actionHandlers.Remove(this);
        }
    }
}
