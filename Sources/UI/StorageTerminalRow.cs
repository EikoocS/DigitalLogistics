using System;
using DigitalStorage.Controller;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace DigitalStorage.UI
{
    internal sealed class StorageTerminalRow : EmptyPixelElement, TooltipListener
    {
        private const float RowHeight = 40;
        private const float PriceColumnWidth = 144;
        private static readonly Color NormalRowColor = new(0.9f, 0.9f, 0.9f, 1);
        private static readonly Color ExpandedGroupColor = new(0.75f, 0.75f, 0.75f, 1);

        private readonly TreeNodeRender label;
        private readonly TreeNodeRender price;
        internal StorageTerminalRowController Controller { get; }

        internal StorageTerminalRow(StorageTerminalRowController controller,
            TreeNodeRender rowParent, RectTransform parent)
        {
            Controller = controller;
            var row = handler.SetParent(rowParent);
            row.gameObject.name = "StorageTerminalItem";
            row.rectTransform.SetParent(parent, false);
            row.image.pixelsPerUnitMultiplier = 1 / CustomUIMachineryStyle.SCALE;
            MachineryStyle.SetSliced(row, MachineryStyle.BevelFrame, NormalRowColor);

            var size = row.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = RowHeight;
            size.flexibleHeight = 0;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 4, 4);
            layout.spacing = 8;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            label = CreateLabel(row, HorizontalAlignmentOptions.Left);
            label.text.richText = true;
            price = CreateLabel(row, HorizontalAlignmentOptions.Right, PriceColumnWidth);
            price.text.color = MachineryStyle.PROGRESS_FILL_WARNING;
        }

        internal static StorageTerminalRowController FindController(Vector3 position) =>
            RenderHandler.RaycastElement<StorageTerminalRow>(position)?.Controller;

        internal void SetIndex(int index)
        {
            if (rectTransform.GetSiblingIndex() != index) rectTransform.SetSiblingIndex(index);
        }

        internal void SetExpanded(bool expanded) =>
            handler.image.color = expanded ? ExpandedGroupColor : NormalRowColor;

        internal void BindEvents(ScrollRect scroll, Action toggle = null)
        {
            handler.InitEventHandler();
            if (toggle != null)
                handler.eventHandler.mouseClickFunc.Add(data =>
                {
                    if (data.button == PointerEventData.InputButton.Left && !data.dragging
                        && !Controller.Owner.Interacting) toggle();
                    return true;
                });
            handler.eventHandler.mouseScrollFunc.Add(data => { scroll.OnScroll(data); return true; });
        }

        internal void Show(string name, int contrabandLevel, int? heat, long value, string priceUnit,
            int? count = null)
        {
            SetName(name, contrabandLevel, heat, count);
            string text = $"{value:N0} {priceUnit}";
            if (price.text.text != text) price.text.text = text;
        }

        internal void Destroy()
        {
            handler.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(handler.gameObject);
        }

        public bool HasTooltip() => Controller.HasTooltip();
        public RichText GetTooltip() => Controller.GetTooltip();
        public RichText GetTooltipAdvanced() => Controller.GetTooltipAdvanced();

        internal RichText CreateGroupTooltip(string name, int count, long totalPrice)
        {
            var builder = new RichTextBuilder(60);
            builder.AddLine($"{name} *{count}");
            builder.AddLine($"{totalPrice:N0} {LocHelper.GetLocalizedMechanic("mech_credits")}");
            return builder.Build();
        }

        private void SetName(string name, int contrabandLevel, int? heat, int? count = null)
        {
            string text = $"<noparse>{name}</noparse>";
            if (count.HasValue) text += $" <b><i>*{count.Value}</i></b>";
            string contraband = contrabandLevel switch
            {
                1 => "[I]",
                2 => "[II]",
                3 => "[III]",
                4 => "[IV]",
                _ => ""
            };
            if (contraband.Length > 0) text += FormatStatus(contraband, (contrabandLevel - 1) / 3f);
            if (heat.HasValue) text += FormatStatus($"[{heat.Value}%]", heat.Value / 100f);
            if (label.text.text != text) label.text.text = text;
        }

        private static string FormatStatus(string text, float severity)
        {
            var color = Color.Lerp(Color.white, MachineryStyle.PROGRESS_FILL_DANGER, severity);
            return $" <color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";
        }

        private static TreeNodeRender CreateLabel(TreeNodeRender parent, HorizontalAlignmentOptions alignment,
            float width = 0)
        {
            var label = TreeNodeRender.InstantiateText().SetParent(parent);
            MachineryStyle.InitText(label, 16, MachineryStyle.BODY_TEXT, alignment, false);
            label.text.richText = false;
            label.text.enableWordWrapping = false;
            label.text.fontSizeMin = 3;
            label.text.fontSizeMax = 16;
            label.text.enableAutoSizing = true;
            label.text.overflowMode = TextOverflowModes.Ellipsis;
            label.text.raycastTarget = false;
            var size = label.gameObject.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = width;
            size.flexibleWidth = width > 0 ? 0 : 1;
            return label;
        }
    }
}
