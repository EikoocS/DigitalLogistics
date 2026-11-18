using System;
using TMPro;
using UnityEngine;

namespace DigitalLogistics.UI.Common
{
    internal static class MachineryControlFactory
    {
        internal static (EmptyPixelElement Host, GameObject Root) CreateControl(
            CustomUIManager.ElementKind kind, int width, int height, bool canResize = false)
        {
            var host = new EmptyPixelElement(width, height, canResize);
            host.handler.image.enabled = false;
            host.rectTransform.sizeDelta = new Vector2(width, height);
            var root = CustomUIManager.Instance.SpawnElement(kind, host.handler.transform, null, kind.ToString());
            CustomUIMachineryStyle.Reskin(root);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width * 2, height * 2);
            rect.localScale = new Vector3(0.5f, 0.5f, 1);
            return (host, root);
        }

        internal static CustomUIElement CreateLabel(RectTransform parent, Vector2 position, string text, float anchor = 0)
        {
            var root = CustomUIManager.Instance.SpawnElement(CustomUIManager.ElementKind.Label, parent, null, "Label");
            CustomUIMachineryStyle.Reskin(root);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(anchor, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(192, 36);
            var element = CustomUIElement.Bind(root, "label");
            element.SetText(text);
            return element;
        }

        internal static int FitLabel(CustomUIElement label)
        {
            label.text.enableAutoSizing = false;
            label.text.enableWordWrapping = false;
            int width = Mathf.CeilToInt(label.text.GetPreferredValues(label.text.text).x);
            ((RectTransform)label.root.transform).sizeDelta = new Vector2(width, 36);
            return width;
        }

        internal static TMP_InputField CreateInput(RectTransform parent, Vector2 position, int width,
            bool right = false, bool stretch = true)
        {
            var root = CustomUIManager.Instance.SpawnElement(CustomUIManager.ElementKind.Input, parent, null, "Input");
            CustomUIMachineryStyle.Reskin(root);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(right ? 1 : 0, 1);
            rect.anchorMax = right || stretch ? Vector2.one : new Vector2(0, 1);
            rect.pivot = new Vector2(right ? 1 : 0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, 36);
            var input = CustomUIElement.Bind(root, "input").input;
            input.lineType = TMP_InputField.LineType.SingleLine;
            ((TMP_Text)input.placeholder).text = string.Empty;
            return input;
        }

        internal static CustomUIElement CreateButton(RectTransform parent, Vector2 position, Vector2 size,
            string text, Action onClick)
        {
            var root = CustomUIManager.Instance.SpawnElement(CustomUIManager.ElementKind.Button, parent, null, "Button");
            CustomUIMachineryStyle.Reskin(root);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var element = CustomUIElement.Bind(root, "button");
            element.SetText(text);
            element.button.onClick.AddListener(() => onClick());
            return element;
        }
    }
}
