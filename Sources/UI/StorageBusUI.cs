using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DigitalStorage.Controller;
using DigitalStorage.Storage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DigitalStorage.UI
{
    internal sealed class StorageBusUI : MonoBehaviour
    {
        private const int Width = 300;
        private const int RowHeight = 96;
        private const int RowGap = 8;
        private const int Padding = 8;
        private const int VisibleRows = 4;

        private sealed class Row
        {
            internal StorageBusManager.Configuration Configuration;
            internal RectTransform Root;
            internal TMP_InputField Name;
            internal TMP_InputField Priority;
            internal TMP_InputField Filter;
        }

        private GameItem bus;
        private ScrollRect scroll;
        private EmptyPixelElement host;
        private RectTransform controls;
        private MachineryIntake intake;
        private MachineryPanel panel;
        private PixelWindow window;
        private readonly List<Row> rows = new();

        internal static PixelWindow Create(GameItem bus)
        {
            var (host, root) = StorageTerminalUI.CreateControl(CustomUIManager.ElementKind.ScrollView, Width, 56, canResize: true);
            var ui = root.AddComponent<StorageBusUI>();
            ui.bus = bus;
            ui.host = host;
            ui.controls = (RectTransform)root.transform;
            ui.scroll = root.GetComponentInChildren<ScrollRect>(true);
            ui.scroll.horizontal = false;
            ui.scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            ui.scroll.verticalScrollbarSpacing = 4;
            if (ui.scroll.horizontalScrollbar != null)
                ui.scroll.horizontalScrollbar.gameObject.SetActive(false);
            var layout = ui.scroll.content.GetComponent<VerticalLayoutGroup>();
            if (layout != null) layout.enabled = false;
            var fitter = ui.scroll.content.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;
            ui.scroll.content.anchorMin = new Vector2(0, 1);
            ui.scroll.content.anchorMax = Vector2.one;
            ui.scroll.content.pivot = new Vector2(0, 1);
            ui.intake = new MachineryIntake(host);
            ui.panel = new MachineryPanel((string)null, ui.intake);
            ui.window = MachineHelper.CreateMachineryWindow(true, ui.panel);
            ui.window.Lock();
            return ui.window;
        }

        private void Update()
        {
            foreach (var row in rows)
            {
                RefreshInput(row.Name, row.Configuration.Name);
                if (!row.Name.isFocused)
                    ((TMP_Text)row.Name.placeholder).text = row.Configuration.DefaultName;
                RefreshInput(row.Priority, row.Configuration.Priority.ToString(CultureInfo.InvariantCulture));
                RefreshInput(row.Filter, row.Configuration.Filter);
            }
        }

        private static void RefreshInput(TMP_InputField input, string value)
        {
            if (!input.isFocused) input.SetTextWithoutNotify(value);
        }

        internal static void RefreshConnections(GameItem bus) =>
            bus.contentWindow.handler.GetComponentInChildren<StorageBusUI>(true).Rebuild();

        private void Rebuild()
        {
            var configurations = StorageBusController.GetConfigurations(bus).ToArray();
            if (rows.Select(row => row.Configuration).SequenceEqual(configurations)) return;
            foreach (var row in rows)
            {
                row.Root.gameObject.SetActive(false);
                Destroy(row.Root.gameObject);
            }
            rows.Clear();
            foreach (var configuration in configurations)
            {
                var root = new GameObject("StorageBusConfiguration", typeof(RectTransform)).GetComponent<RectTransform>();
                root.SetParent(scroll.content, false);
                root.anchorMin = root.pivot = new Vector2(0, 1);
                root.anchorMax = Vector2.one;
                root.anchoredPosition = new Vector2(Padding, -Padding - rows.Count * (RowHeight + RowGap));
                root.sizeDelta = new Vector2(-Padding * 2, RowHeight);
                var background = root.gameObject.AddComponent<Image>();
                background.sprite = MachineryStyle.BevelFrame;
                background.type = Image.Type.Sliced;
                background.pixelsPerUnitMultiplier = 0.5f;
                background.color = Color.white;
                background.raycastTarget = false;
                var row = new Row
                {
                    Configuration = configuration,
                    Root = root,
                    Name = CreateInput(root, new Vector2(Padding, -Padding), -144),
                    Priority = CreateInput(root, new Vector2(-Padding, -Padding), 120, right: true),
                    Filter = CreateInput(root, new Vector2(Padding, -52), -Padding * 2)
                };
                row.Priority.contentType = TMP_InputField.ContentType.IntegerNumber;
                row.Name.textComponent.color = Color.white;
                var nameTip = (TMP_Text)row.Name.placeholder;
                nameTip.text = configuration.DefaultName;
                nameTip.color = Color.gray;
                nameTip.fontStyle = FontStyles.Normal;
                row.Name.onSelect.AddListener(_ => nameTip.text = string.Empty);
                row.Name.onDeselect.AddListener(_ => nameTip.text = configuration.DefaultName);
                row.Name.SetTextWithoutNotify(configuration.Name);
                row.Priority.SetTextWithoutNotify(configuration.Priority.ToString(CultureInfo.InvariantCulture));
                row.Filter.SetTextWithoutNotify(configuration.Filter);
                row.Name.onEndEdit.AddListener(value =>
                {
                    configuration.Name = value;
                    configuration.Apply();
                });
                row.Priority.onEndEdit.AddListener(value =>
                {
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int priority))
                    {
                        configuration.Priority = priority;
                        configuration.Apply();
                    }
                });
                row.Filter.onValueChanged.AddListener(_ => row.Filter.textComponent.color = Color.white);
                row.Filter.onEndEdit.AddListener(value =>
                {
                    var expression = string.IsNullOrWhiteSpace(value)
                        ? Filtering.StoredItemExpression.Constant(true) : ParseFilter(value);
                    row.Filter.textComponent.color = expression == null ? Color.red : Color.white;
                    if (expression == null) return;
                    configuration.Filter = value;
                    if (configuration.Provider != null) configuration.Provider.filter = expression;
                });
                rows.Add(row);
            }
            ResizeWindow();
        }

        private static int ContentHeight(int count) =>
            Padding * 2 + count * RowHeight + Mathf.Max(0, count - 1) * RowGap;

        private void ResizeWindow()
        {
            bool overflow = rows.Count > VisibleRows;
            int height = ContentHeight(Mathf.Min(rows.Count, VisibleRows));
            host.ResizePixels(Width, height / 2);
            host.rectTransform.sizeDelta = new Vector2(Width, height / 2);
            controls.sizeDelta = new Vector2(Width * 2, height);
            scroll.viewport.anchorMin = Vector2.zero;
            scroll.viewport.anchorMax = Vector2.one;
            scroll.viewport.offsetMin = Vector2.zero;
            scroll.viewport.offsetMax = new Vector2(overflow ? -20 : 0, 0);
            if (scroll.verticalScrollbar != null)
            {
                scroll.verticalScrollbar.gameObject.SetActive(overflow);
                var bar = (RectTransform)scroll.verticalScrollbar.transform;
                bar.anchorMin = new Vector2(1, 0);
                bar.anchorMax = Vector2.one;
                bar.pivot = new Vector2(1, 1);
                bar.offsetMin = new Vector2(-16, 0);
                bar.offsetMax = Vector2.zero;
            }
            scroll.content.sizeDelta = new Vector2(0, ContentHeight(rows.Count));
            scroll.StopMovement();
            scroll.content.anchoredPosition = Vector2.zero;
            intake.ResizePixels(0, 0);
            panel.ResizePixels(0, 0);
            window.Validate();
            if (window.IsVisible()) window.FixBounds();
        }

        private static Filtering.StoredItemExpression ParseFilter(string text)
        {
            try { return Filtering.StoredItemExpressionParser.Parse(text); }
            catch (Superpower.ParseException) { return null; }
        }

        private static TMP_InputField CreateInput(RectTransform parent, Vector2 position, int width, bool right = false)
        {
            var root = CustomUIManager.Instance.SpawnElement(CustomUIManager.ElementKind.Input, parent, null, "Input");
            CustomUIMachineryStyle.Reskin(root);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(right ? 1 : 0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(right ? 1 : 0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, 36);
            var input = CustomUIElement.Bind(root, "input").input;
            input.lineType = TMP_InputField.LineType.SingleLine;
            ((TMP_Text)input.placeholder).text = string.Empty;
            return input;
        }
    }
}
