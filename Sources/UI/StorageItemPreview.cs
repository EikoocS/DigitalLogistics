using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DigitalStorage.UI
{
    internal sealed class StorageItemPreview
    {
        private readonly RectTransform root;

        internal StorageItemPreview(GameItemElement item)
        {
            root = new GameObject("StorageItemPreview", typeof(RectTransform), typeof(CanvasGroup))
                .GetComponent<RectTransform>();
            var parent = RenderHandler.current.overlayRoot.rectTransform;
            root.SetParent(parent, false);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            var scale = item.middle.rectTransform.lossyScale;
            root.localScale = new Vector3(scale.x / parent.lossyScale.x, scale.y / parent.lossyScale.y, 1);
            var group = root.GetComponent<CanvasGroup>();
            group.alpha = UISettings.current.dragOpacity;
            group.blocksRaycasts = group.interactable = false;
            var visual = CopyVisual(item.rotator.rectTransform, root);
            visual.anchorMin = visual.anchorMax = visual.pivot = new Vector2(0.5f, 0.5f);
            visual.anchoredPosition = Vector2.zero;
        }

        internal void Move(Vector3 position) => root.position = position;

        internal void Destroy()
        {
            root.gameObject.SetActive(false);
            Object.Destroy(root.gameObject);
        }

        private static RectTransform CopyVisual(RectTransform source, RectTransform parent)
        {
            var rect = new GameObject(source.name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot;
            rect.sizeDelta = source.sizeDelta;
            rect.anchoredPosition3D = source.anchoredPosition3D;
            rect.localRotation = source.localRotation;
            rect.localScale = source.localScale;

            var image = source.GetComponent<Image>();
            if (image != null && image.enabled)
            {
                var copy = rect.gameObject.AddComponent<Image>();
                copy.sprite = image.sprite;
                copy.color = image.color;
                copy.material = image.material;
                copy.type = image.type;
                copy.preserveAspect = image.preserveAspect;
                copy.fillMethod = image.fillMethod;
                copy.fillOrigin = image.fillOrigin;
                copy.fillAmount = image.fillAmount;
                copy.fillClockwise = image.fillClockwise;
                copy.pixelsPerUnitMultiplier = image.pixelsPerUnitMultiplier;
                copy.raycastTarget = false;
            }

            var text = source.GetComponent<TMP_Text>();
            if (text != null && text.enabled)
            {
                var copy = rect.gameObject.AddComponent<TextMeshProUGUI>();
                copy.font = text.font;
                copy.fontSharedMaterial = text.fontSharedMaterial;
                copy.text = text.text;
                copy.fontSize = text.fontSize;
                copy.fontStyle = text.fontStyle;
                copy.color = text.color;
                copy.alignment = text.alignment;
                copy.raycastTarget = false;
            }

            var mask = source.GetComponent<RectMask2D>();
            if (mask != null && mask.enabled)
                rect.gameObject.AddComponent<RectMask2D>().padding = mask.padding;

            foreach (RectTransform child in source)
                if (child.gameObject.activeSelf) CopyVisual(child, rect);
            return rect;
        }
    }
}
