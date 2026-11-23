using DigitalLogistics.Controller;
using UnityEngine;

namespace DigitalLogistics.UI
{
    internal sealed class StorageTerminalDragUI
    {
        private StorageItemPreview preview;
        private TreeNodeRender inputShield;
        private readonly HighlightManager highlights = new();

        internal void ShowPreview(GameItem item, GridShape shape)
        {
            preview?.Destroy();
            preview = new StorageItemPreview((GameItemElement)item, shape);
            UpdatePreviewPosition();
        }

        internal void SetShape(GridShape shape) => preview.SetShape(shape);

        internal void HidePreview()
        {
            preview?.Destroy();
            preview = null;
        }

        internal void Update()
        {
            OverlayHandler.current.HideTooltip();
            if (inputShield != null && TryGetOverlayPoint(Vector2.zero, out var min)
                && TryGetOverlayPoint(new Vector2(Screen.width, Screen.height), out var max))
            {
                inputShield.rectTransform.localPosition = (min + max) * 0.5f;
                inputShield.rectTransform.sizeDelta = max - min;
            }
            UpdatePreviewPosition();
        }

        private void UpdatePreviewPosition()
        {
            if (preview != null && TryGetOverlayPoint(Input.mousePosition, out var position))
                preview.SetOverlayPosition(position);
        }

        private static bool TryGetOverlayPoint(Vector2 screenPosition, out Vector2 position)
        {
            var render = RenderHandler.current;
            var canvas = render.OverlayCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(render.overlayRoot.rectTransform,
                screenPosition, camera, out position);
        }

        internal void Highlight(SlotMarker slot) => highlights.Try(slot, slot?.numTransfer > 0
            ? RenderHandler.BackgroundColor.Green : RenderHandler.BackgroundColor.Red);

        internal void ClearHighlight() => highlights.Clear();

        internal StorageTerminalRowController FindRow(Vector3 position) =>
            RenderHandler.RaycastElement<StorageTerminalRow>(position, inputShield)?.Controller;

        internal GameItem FindItem(Vector3 position) =>
            RenderHandler.RaycastElement<GameItem>(position, inputShield);

        internal GameInventory FindInventory(Vector3 position) =>
            RenderHandler.RaycastFindParentElement<GameInventory>(position, inputShield);

        internal void ShowInputShield()
        {
            if (inputShield != null) return;
            inputShield = TreeNodeRender.Instantiate().SetParentOverlayRoot();
            inputShield.gameObject.name = "StorageTerminalInputShield";
            inputShield.InitEventHandler();
            inputShield.image.enabled = true;
            inputShield.image.alphaHitTestMinimumThreshold = 0;
            inputShield.image.color = Color.clear;
            inputShield.rectTransform.anchorMin = inputShield.rectTransform.anchorMax =
                inputShield.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        internal bool IsOverTerminal(StorageTerminalController owner, Vector3 position) =>
            owner.ContainsPointer(position, inputShield);

        internal bool IsOverTerminalWindow(Vector3 position)
        {
            var window = RenderHandler.RaycastFindParentElement<PixelWindow>(position, inputShield);
            return window?.handler.GetComponentInChildren<StorageTerminalController>() != null;
        }

        internal void HideInputShield()
        {
            if (inputShield == null) return;
            inputShield.gameObject.SetActive(false);
            Object.Destroy(inputShield.gameObject);
            inputShield = null;
        }
    }
}
