using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DigitalStorage.Controller;
using DigitalStorage.Item;
using DigitalStorage.Storage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DigitalStorage.UI
{
    internal sealed class BusUI : MonoBehaviour
    {
        private const int Width = 360;
        private const int RowGap = 8;
        private const int Padding = 8;
        private int VisibleRows => bus.identifier == IOBus.InputOutputId ? 3 : 4;

        private sealed class Row
        {
            internal object Configuration;
            internal RectTransform Root;
        }

        private GameItem bus;
        private ScrollRect scroll;
        private EmptyPixelElement host;
        private RectTransform controls;
        private MachineryIntake intake;
        private MachineryPanel panel;
        private PixelWindow window;
        private readonly List<Row> rows = new();
        private readonly List<Action> refreshers = new();
        private readonly List<Action> globalRefreshers = new();
        private bool IsIO => IOBus.IsIO(bus);
        private int RowHeight => !IsIO ? 96 : bus.identifier == IOBus.InputOutputId ? 184 : 140;

        internal static PixelWindow Create(GameItem bus)
        {
            var (host, root) = StorageTerminalUI.CreateControl(CustomUIManager.ElementKind.ScrollView, Width, 56, canResize: true);
            var ui = root.AddComponent<BusUI>();
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
            PixelElement content = host;
            if (ui.IsIO)
            {
                var onAdd = ui.CreateToggle("on_add", IOBusManager.OnAddTag);
                var onMove = ui.CreateToggle("on_move", IOBusManager.OnMoveTag);
                content = MachineryStack.Vertical(onAdd, onMove, host)
                    .SetAlign(MachineryStack.Align.Start).SetMainAlign(MachineryStack.Align.Start);
            }
            ui.intake = new MachineryIntake(content);
            ui.panel = new MachineryPanel((string)null, ui.intake);
            ui.window = MachineHelper.CreateMachineryWindow(true, ui.panel);
            if (!ui.IsIO) ui.window.Lock();
            ui.Rebuild();
            return ui.window;
        }

        private void Update()
        {
            foreach (var refresh in globalRefreshers) refresh();
            foreach (var refresh in refreshers) refresh();
        }

        private EmptyPixelElement CreateToggle(string caption, string tag)
        {
            var (toggleHost, root) = StorageTerminalUI.CreateControl(CustomUIManager.ElementKind.Toggle, Width, 22);
            var element = CustomUIElement.Bind(root, caption);
            element.SetText(StorageTerminal.Localize("ui.io_bus." + caption));
            globalRefreshers.Add(() => element.toggle.SetIsOnWithoutNotify(bus.GetTagReadonly(tag).GetBool()));
            element.toggle.onValueChanged.AddListener(value =>
                bus.ModifyTag(tag, state => state.Enable().SetBool(value)));
            return toggleHost;
        }

        private static void RefreshInput(TMP_InputField input, string value)
        {
            if (!input.isFocused) input.SetTextWithoutNotify(value);
        }

        internal static void RefreshConnections(GameItem bus) =>
            bus.contentWindow.handler.GetComponentInChildren<BusUI>(true).Rebuild();

        private void Rebuild()
        {
            var configurations = IsIO ? IOBusManager.GetConfigurations(bus).Cast<object>().ToArray()
                : BusController.GetConfigurations(bus).Cast<object>().ToArray();
            if (rows.Select(row => row.Configuration).SequenceEqual(configurations)) return;
            foreach (var row in rows)
            {
                row.Root.gameObject.SetActive(false);
                Destroy(row.Root.gameObject);
            }
            rows.Clear();
            refreshers.Clear();
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
                if (configuration is BusManager.Configuration storage) BuildStorageRow(root, storage);
                else BuildIORow(root, (IOBusManager.Configuration)configuration);
                rows.Add(new Row { Configuration = configuration, Root = root });
            }
            ResizeWindow();
        }

        private int ContentHeight(int count) =>
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

        private void BuildStorageRow(RectTransform root, BusManager.Configuration configuration)
        {
            BindText(CreateInput(root, new Vector2(Padding + 72, -Padding), -216),
                () => configuration.Name, () => configuration.DefaultName, value =>
                {
                    configuration.Name = value;
                    configuration.Apply();
                });
            BindInteger(CreateInput(root, new Vector2(-Padding, -Padding), 120, right: true),
                () => configuration.Priority, value =>
                {
                    configuration.Priority = value;
                    configuration.Apply();
                });
            BindFilter(CreateInput(root, new Vector2(Padding, -52), -Padding * 2),
                () => configuration.Filter, () => BusManager.Configuration.DefaultFilter, value =>
                {
                    configuration.Filter = value;
                    configuration.Apply();
                });
            BindEnabled(root, () => !configuration.Disabled, value =>
            {
                configuration.Disabled = !value;
                configuration.Apply();
            });
        }

        private void BuildIORow(RectTransform root, IOBusManager.Configuration configuration)
        {
            BindText(CreateInput(root, new Vector2(Padding + 72, -Padding), -Padding * 2 - 72),
                () => configuration.Name, () => configuration.DefaultName, value => configuration.Name = value);
            int y = 52;
            if (IOBus.CanInput(bus))
            {
                BindOperation(root, y, "input", () => configuration.InputOrder,
                    value => configuration.InputOrder = value, () => configuration.InputPriority,
                    value => configuration.InputPriority = value);
                y += 44;
            }
            if (IOBus.CanOutput(bus))
            {
                BindOperation(root, y, "output", () => configuration.OutputOrder,
                    value => configuration.OutputOrder = value, () => configuration.OutputPriority,
                    value => configuration.OutputPriority = value);
                y += 44;
            }
            BindFilter(CreateInput(root, new Vector2(Padding, -y), -Padding * 2),
                () => configuration.Filter, () => configuration.DefaultFilter, value =>
                {
                    configuration.Filter = value;
                    configuration.Apply();
                });
            BindEnabled(root, () => !configuration.Disabled, value => configuration.Disabled = !value);
        }

        private void BindEnabled(RectTransform root, Func<bool> get, Action<bool> set)
        {
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var inputs = root.GetComponentsInChildren<TMP_InputField>(true);
            var switchRoot = new GameObject("Enabled", typeof(RectTransform), typeof(Image), typeof(Toggle));
            var rect = (RectTransform)switchRoot.transform;
            rect.SetParent(root, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(Padding, -Padding);
            rect.sizeDelta = new Vector2(64, 36);
            var track = switchRoot.GetComponent<Image>();
            track.sprite = MachineryStyle.BevelFrame;
            track.type = Image.Type.Sliced;
            track.pixelsPerUnitMultiplier = 0.5f;

            var thumbRoot = new GameObject("Thumb", typeof(RectTransform), typeof(Image));
            var thumbRect = (RectTransform)thumbRoot.transform;
            thumbRect.SetParent(rect, false);
            thumbRect.anchorMin = thumbRect.anchorMax = thumbRect.pivot = new Vector2(0, 0.5f);
            thumbRect.sizeDelta = new Vector2(24, 24);
            var thumb = thumbRoot.GetComponent<Image>();
            thumb.sprite = MachineryStyle.BevelFrame;
            thumb.type = Image.Type.Sliced;
            thumb.pixelsPerUnitMultiplier = 0.5f;
            thumb.raycastTarget = false;
            var toggle = switchRoot.GetComponent<Toggle>();
            toggle.targetGraphic = thumb;

            void Refresh()
            {
                bool enabled = get();
                toggle.SetIsOnWithoutNotify(enabled);
                thumbRect.anchoredPosition = new Vector2(enabled ? 34 : 6, 0);
                track.color = enabled ? MachineryStyle.PROGRESS_FILL : MachineryStyle.SLIDER_FILL;
                group.alpha = enabled ? 1 : 0.45f;
                foreach (var input in inputs) input.interactable = enabled;
            }
            toggle.onValueChanged.AddListener(value =>
            {
                set(value);
                Refresh();
            });
            refreshers.Add(Refresh);
            Refresh();
        }

        private void BindOperation(RectTransform root, int y, string direction, Func<int> order,
            Action<int> setOrder, Func<int> priority, Action<int> setPriority)
        {
            CreateLabel(root, new Vector2(Padding, -y), direction + "_order");
            CreateLabel(root, new Vector2(Padding, -y), direction + "_priority", anchor: 0.5f);
            var orderInput = CreateInput(root, new Vector2(208, -y), 128, stretch: false);
            BindInteger(orderInput, order, value => setOrder(Mathf.Clamp(value, 1, 15)));
            var priorityInput = CreateInput(root, new Vector2(-Padding, -y), 128, right: true);
            BindInteger(priorityInput, priority, setPriority);
        }

        private static void CreateLabel(RectTransform parent, Vector2 position, string key, float anchor = 0)
        {
            var root = CustomUIManager.Instance.SpawnElement(CustomUIManager.ElementKind.Label, parent, null, key);
            CustomUIMachineryStyle.Reskin(root);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(anchor, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(192, 36);
            CustomUIElement.Bind(root, key).SetText(StorageTerminal.Localize("ui.io_bus." + key));
        }

        private void BindInteger(TMP_InputField input, Func<int> get, Action<int> set)
        {
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            BindText(input, () => get().ToString(CultureInfo.InvariantCulture), () => string.Empty, value =>
            {
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)) set(number);
            });
        }

        private void BindFilter(TMP_InputField input, Func<string> get, Func<string> defaultValue, Action<string> set)
        {
            BindText(input, get, defaultValue, value =>
            {
                var expression = ParseFilter(string.IsNullOrWhiteSpace(value) ? defaultValue() : value);
                input.textComponent.color = expression == null ? Color.red : Color.white;
                if (expression != null) set(value);
            });
            input.onValueChanged.AddListener(_ => input.textComponent.color = Color.white);
        }

        private void BindText(TMP_InputField input, Func<string> get, Func<string> defaultValue, Action<string> set)
        {
            input.textComponent.color = Color.white;
            var placeholder = (TMP_Text)input.placeholder;
            placeholder.color = Color.gray;
            placeholder.fontStyle = FontStyles.Normal;
            input.onSelect.AddListener(_ => placeholder.text = string.Empty);
            input.onDeselect.AddListener(_ => placeholder.text = defaultValue());
            input.onEndEdit.AddListener(value => set(value));
            void Refresh()
            {
                RefreshInput(input, get());
                if (!input.isFocused) placeholder.text = defaultValue();
            }
            refreshers.Add(Refresh);
            Refresh();
        }

        private static TMP_InputField CreateInput(RectTransform parent, Vector2 position, int width,
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
    }
}
