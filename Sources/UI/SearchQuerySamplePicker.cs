using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DigitalLogistics.UI
{
    internal sealed class SearchQuerySamplePicker : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private readonly HighlightManager highlights = new();
        internal Action<GameItem> Sampled;

        public void OnBeginDrag(PointerEventData data)
        {
            data.eligibleForClick = false;
        }

        public void OnDrag(PointerEventData data)
        {
            var item = RenderHandler.RaycastElement<GameItem>(data.position);
            highlights.Try(item?.GetIncompleteSlotInInventory(), RenderHandler.BackgroundColor.Green);
        }

        public void OnEndDrag(PointerEventData data)
        {
            highlights.Clear();
            var item = RenderHandler.RaycastElement<GameItem>(data.position);
            if (item != null) Sampled(item);
        }

        private void OnDisable() => highlights.Clear();
    }
}
