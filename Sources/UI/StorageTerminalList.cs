using System;
using DigitalLogistics.Controller;
using UnityEngine;
using UnityEngine.UI;

namespace DigitalLogistics.UI
{
    internal sealed class StorageTerminalList
    {
        private const float RowSpacing = 4;
        private readonly ScrollRect scroll;
        private readonly TreeNodeRender rowParent;
        private readonly Vector2 contentOffsetMin;
        private readonly Vector2 contentOffsetMax;
        private readonly RectOffset contentPadding;
        private string priceUnit;
        internal float ScrollPosition => scroll.content.anchoredPosition.y;

        internal sealed class GroupView
        {
            private readonly StorageTerminalList list;
            private readonly RectTransform content;
            private RectTransform root;
            private RectTransform children;
            internal StorageTerminalRow Header { get; }

            internal GroupView(StorageTerminalList list, RectTransform content, StorageTerminalRow header)
            {
                this.list = list;
                this.content = content;
                Header = header;
                root = header.rectTransform;
            }

            internal void MakeGroup(bool expanded, Action toggle)
            {
                int index = root.GetSiblingIndex();
                root = CreateContainer(content, "StorageTerminalGroup");
                root.SetSiblingIndex(index);
                Header.rectTransform.SetParent(root, false);
                children = CreateContainer(root, "StorageTerminalChildren");
                children.GetComponent<VerticalLayoutGroup>().padding.left = 20;
                SetExpanded(expanded);
                Header.BindEvents(list.scroll, toggle);
            }

            internal void MakeSingle()
            {
                int index = root.GetSiblingIndex();
                Header.rectTransform.SetParent(content, false);
                Header.SetIndex(index);
                Destroy();
                root = Header.rectTransform;
                children = null;
                Header.BindEvents(list.scroll);
                Header.SetExpanded(false);
            }

            internal StorageTerminalRow CreateRow(StorageTerminalRowController controller) =>
                list.CreateRow(controller, children);

            internal void SetExpanded(bool expanded)
            {
                children.gameObject.SetActive(expanded);
                Header.SetExpanded(expanded);
            }

            internal void SetIndex(int index)
            {
                if (root.GetSiblingIndex() != index) root.SetSiblingIndex(index);
            }

            internal void Destroy()
            {
                root.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(root.gameObject);
            }
        }

        internal StorageTerminalList(GameObject root, TreeNodeRender rowParent)
        {
            this.rowParent = rowParent;
            scroll = root.GetComponentInChildren<ScrollRect>(true);
            scroll.horizontal = false;
            ConfigureContent(scroll.content);
            contentOffsetMin = scroll.content.offsetMin;
            contentOffsetMax = scroll.content.offsetMax;
            contentPadding = scroll.content.GetComponent<VerticalLayoutGroup>().padding;
        }

        internal RectTransform CreateStagingContent()
        {
            var root = new GameObject("StorageTerminalContent", typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter), typeof(CanvasGroup));
            var canvas = root.GetComponent<CanvasGroup>();
            canvas.alpha = 0;
            canvas.blocksRaycasts = canvas.interactable = false;
            var content = (RectTransform)root.transform;
            content.SetParent(scroll.content.parent, false);
            ConfigureContent(content);
            content.GetComponent<VerticalLayoutGroup>().padding = contentPadding;
            content.offsetMin = contentOffsetMin;
            content.offsetMax = contentOffsetMax;
            content.anchoredPosition = Vector2.zero;
            return content;
        }

        internal void Publish(RectTransform content, float position)
        {
            var previous = scroll.content;
            scroll.content = content;
            RestoreScroll(position);
            Discard(previous);
            var canvas = content.GetComponent<CanvasGroup>();
            canvas.alpha = 1;
            canvas.blocksRaycasts = canvas.interactable = true;
        }

        internal static void Discard(RectTransform content)
        {
            if (content == null) return;
            content.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(content.gameObject);
        }

        internal GroupView CreateRow(RectTransform content, StorageTerminalRowController controller) =>
            new GroupView(this, content, CreateRow(controller, content));

        private StorageTerminalRow CreateRow(StorageTerminalRowController controller, RectTransform parent)
        {
            var row = new StorageTerminalRow(controller, rowParent, parent);
            row.BindEvents(scroll);
            return row;
        }

        internal void RefreshPriceUnit() => priceUnit = LocHelper.GetLocalizedMechanic("mech_credits");

        private void RestoreScroll(float position)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            float maximum = Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height);
            scroll.StopMovement();
            scroll.content.anchoredPosition = new Vector2(0, Mathf.Clamp(position, 0, maximum));
        }

        internal void ShowRow(StorageTerminalRow row, string name, int contrabandLevel, int? heat,
            long price, int? count = null) => row.Show(name, contrabandLevel, heat, price, priceUnit, count);

        private static RectTransform CreateContainer(RectTransform parent, string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            var rect = (RectTransform)root.transform;
            rect.SetParent(parent, false);
            ConfigureLayout(root.GetComponent<VerticalLayoutGroup>());
            return rect;
        }

        private static void ConfigureContent(RectTransform content)
        {
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            ConfigureLayout(content.GetComponent<VerticalLayoutGroup>());
            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static void ConfigureLayout(VerticalLayoutGroup layout)
        {
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = RowSpacing;
        }
    }
}
