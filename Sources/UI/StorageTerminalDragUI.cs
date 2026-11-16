using DigitalStorage.Controller;
using UnityEngine;

namespace DigitalStorage.UI
{
    internal sealed class StorageTerminalDragUI
    {
        private StorageItemPreview preview;
        private TreeNodeRender inputShield;
        private readonly HighlightManager highlights = new();

        internal void ShowPreview(GameItem item)
        {
            preview?.Destroy();
            preview = new StorageItemPreview((GameItemElement)item);
            preview.Move(Input.mousePosition);
        }

        internal void HidePreview()
        {
            preview?.Destroy();
            preview = null;
        }

        internal void Update()
        {
            OverlayHandler.current.HideTooltip();
            if (inputShield != null)
            {
                inputShield.rectTransform.position = new Vector3(Screen.width / 2f, Screen.height / 2f);
                var scale = inputShield.rectTransform.lossyScale;
                inputShield.rectTransform.sizeDelta = new Vector2(Screen.width / scale.x, Screen.height / scale.y);
            }
            preview?.Move(Input.mousePosition);
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
            inputShield = TreeNodeRender.Instantiate().SetParentOverlayRoot();
            inputShield.gameObject.name = "StorageTerminalInputShield";
            inputShield.InitEventHandler();
            inputShield.image.enabled = true;
            inputShield.image.alphaHitTestMinimumThreshold = 0;
            inputShield.image.color = Color.clear;
            inputShield.rectTransform.anchorMin = inputShield.rectTransform.anchorMax =
                inputShield.rectTransform.pivot = new Vector2(0.5f, 0.5f);
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
