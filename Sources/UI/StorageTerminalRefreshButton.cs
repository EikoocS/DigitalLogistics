using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DigitalStorage.UI
{
    internal sealed class StorageTerminalRefreshButton : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private const double HoldSeconds = 0.65;
        private double pressedAt;
        private bool pressed;
        private bool held;
        private Button button;
        private Image icon;
        private Sprite normalSprite;
        private Sprite pendingSprite;
        private bool pending;

        internal event Action Clicked;
        internal event Action Held;

        internal void Init(Button button)
        {
            this.button = button;
            string prefix = DigitalStorage.Instance.Manifest.ID + ":storage_terminal_refresh";
            normalSprite = CustomUIManager.Instance.GetSprite(prefix);
            pendingSprite = CustomUIManager.Instance.GetSprite(prefix + "_pending");
            var root = new GameObject("RefreshIcon", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)root.transform;
            rect.SetParent(button.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(32, 32);
            icon = root.GetComponent<Image>();
            icon.raycastTarget = false;
            icon.sprite = normalSprite;
        }

        internal void SetPending(bool value)
        {
            if (pending == value) return;
            pending = value;
            icon.sprite = value ? pendingSprite : normalSprite;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || !button.interactable) return;
            pressedAt = Time.realtimeSinceStartupAsDouble;
            pressed = true;
            held = false;
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || !pressed) return;
            pressed = false;
            if (!held && button.interactable) Clicked?.Invoke();
        }

        public void OnPointerExit(PointerEventData data) => pressed = false;

        private void Update()
        {
            if (!pressed || held || !button.interactable
                || Time.realtimeSinceStartupAsDouble - pressedAt < HoldSeconds) return;
            held = true;
            Held?.Invoke();
        }

        private void OnDisable() => pressed = false;
    }
}
