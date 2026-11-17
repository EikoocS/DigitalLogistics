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
        private const int VisibleRows = 4;

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
        private GameObject emptyState;
        private readonly List<Row> rows = new();
        private readonly List<Action> refreshers = new();
        private readonly List<Action> globalRefreshers = new();
        private bool IsIO => IOBus.IsIO(bus);
        private int DefaultRowHeight => bus.identifier switch
        {
            StorageBus.ItemId => 96,
            IOBus.InputOutputId => 184,
            TriggerBus.ItemId => 184,
            _ => 140
        };
        private int RowHeight(object configuration) => configuration is TriggerBusManager.Configuration trigger
            && trigger.Options.Length > 0 ? 228 : DefaultRowHeight;

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
            ui.emptyState = ui.CreateEmptyState();
            PixelElement content = host;
            if (ui.IsIO)
            {
                var onAdd = ui.CreateToggle("ui.io_bus.on_add", IOBusManager.OnAddTag);
                var onMove = ui.CreateToggle("ui.io_bus.on_move", IOBusManager.OnMoveTag);
                content = MachineryStack.Vertical(onAdd, onMove, host)
                    .SetAlign(MachineryStack.Align.Start).SetMainAlign(MachineryStack.Align.Start);
            }
            ui.intake = new MachineryIntake(content);
            ui.panel = new MachineryPanel((string)null, ui.intake);
            ui.window = MachineHelper.CreateMachineryWindow(true, ui.panel);
            ui.Rebuild();
            ui.ResizeWindow();
            return ui.window;
        }

        private void Update()
        {
            foreach (var refresh in globalRefreshers) refresh();
            foreach (var refresh in refreshers) refresh();
        }

        private GameObject CreateEmptyState()
        {
            var root = CustomUIManager.Instance.SpawnElement(
                CustomUIManager.ElementKind.Label, scroll.content, null, "EmptyState");
            CustomUIMachineryStyle.Reskin(root);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var label = CustomUIElement.Bind(root, "empty");
            label.SetText(StorageTerminal.Localize("ui.bus.no_connections"));
            label.text.alignment = TextAlignmentOptions.Center;
            label.text.enableAutoSizing = false;
            label.text.richText = false;
            label.text.raycastTarget = false;
            return root;
        }

        private MachineryRadioButton CreateToggle(string key, string tag)
        {
            var toggle = new MachineryRadioButton(StorageTerminal.Localize(key),
                bus.GetTagReadonly(tag).GetBool(), value =>
                    bus.ModifyTag(tag, state => state.Enable().SetBool(value)));
            globalRefreshers.Add(() => toggle.SetOn(bus.GetTagReadonly(tag).GetBool()));
            return toggle;
        }

        private static void RefreshInput(TMP_InputField input, string value)
        {
            if (!input.isFocused) input.SetTextWithoutNotify(value);
        }

        internal static void RefreshConnections(GameItem bus) =>
            bus.contentWindow.handler.GetComponentInChildren<BusUI>(true).Rebuild();

        private void Rebuild()
        {
            var configurations = BusController.GetConfigurations(bus).ToArray();
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
                root.anchoredPosition = new Vector2(Padding,
                    -Padding - rows.Sum(row => RowHeight(row.Configuration) + RowGap));
                root.sizeDelta = new Vector2(-Padding * 2, RowHeight(configuration));
                var background = root.gameObject.AddComponent<Image>();
                background.sprite = MachineryStyle.BevelFrame;
                background.type = Image.Type.Sliced;
                background.pixelsPerUnitMultiplier = 0.5f;
                background.color = Color.white;
                background.raycastTarget = false;
                if (configuration is BusManager.Configuration storage) BuildStorageRow(root, storage);
                else if (configuration is TriggerBusManager.Configuration trigger) BuildTriggerRow(root, trigger);
                else BuildIORow(root, (IOBusManager.Configuration)configuration);
                rows.Add(new Row { Configuration = configuration, Root = root });
            }
            ResizeWindow();
        }

        private int ContentHeight(int count) =>
            Padding * 2 + (count == 0 ? DefaultRowHeight : rows.Take(count).Sum(row => RowHeight(row.Configuration)))
                + Mathf.Max(0, count - 1) * RowGap;

        private void ResizeWindow()
        {
            emptyState.SetActive(rows.Count == 0);
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

        private void BuildTriggerRow(RectTransform root, TriggerBusManager.Configuration configuration)
        {
            var label = CreateLabel(root, new Vector2(Padding + 72, -Padding), "");
            ((RectTransform)label.root.transform).sizeDelta = new Vector2(Width * 2 - Padding * 4 - 72, 36);
            label.text.enableWordWrapping = false;
            label.text.overflowMode = TextOverflowModes.Ellipsis;
            label.text.richText = false;
            refreshers.Add(() => label.SetText(configuration.Name));
            label.SetText(configuration.Name);
            BindOperation(root, 52, "trigger", () => configuration.Order,
                value => configuration.Order = value, () => configuration.Priority,
                value => configuration.Priority = value);
            CreateLabel(root, new Vector2(Padding, -96), "trigger_count");
            var countInput = CreateInput(root, new Vector2(-Padding, -96), 128, right: true);
            BindInteger(countInput, () => configuration.Count, value => configuration.Count = Mathf.Max(0, value));
            var inputToggle = BindSwitch(root, new Vector2(Padding, -140), () => configuration.OnInputUpdate,
                value => configuration.OnInputUpdate = value);
            var inputLabel = CreateLabel(root, new Vector2(80, -140), "");
            inputLabel.SetText(StorageTerminal.Localize("ui.trigger_bus.on_input_update"));
            inputLabel.text.raycastTarget = false;
            var loopToggle = BindSwitch(root, new Vector2(360, -140), () => configuration.LoopTrigger,
                value => configuration.LoopTrigger = value);
            var loopLabel = CreateLabel(root, new Vector2(432, -140), "");
            loopLabel.SetText(StorageTerminal.Localize("ui.trigger_bus.loop_trigger"));
            loopLabel.text.raycastTarget = false;
            var options = configuration.Options;
            if (options.Length > 0)
            {
                var dropdownRoot = CustomUIManager.Instance.SpawnElement(
                    CustomUIManager.ElementKind.Dropdown, root, null, "TriggerOption");
                CustomUIMachineryStyle.Reskin(dropdownRoot);
                var rect = (RectTransform)dropdownRoot.transform;
                rect.anchorMin = rect.pivot = new Vector2(0, 1);
                rect.anchorMax = Vector2.one;
                rect.anchoredPosition = new Vector2(Padding, -184);
                rect.sizeDelta = new Vector2(-Padding * 2, 36);
                var dropdown = CustomUIElement.Bind(dropdownRoot, "option").dropdown;
                dropdown.ClearOptions();
                dropdown.AddOptions(options.Select(option => option.Name).ToList());
                dropdown.onValueChanged.AddListener(index => configuration.OptionId = options[index].Id);
                void RefreshOption() => dropdown.SetValueWithoutNotify(
                    Array.FindIndex(options, option => option.Id == configuration.OptionId));
                refreshers.Add(RefreshOption);
                RefreshOption();
            }
            BindEnabled(root, () => !configuration.Disabled, value => configuration.Disabled = !value);
            refreshers.Add(() =>
            {
                countInput.interactable = !configuration.Disabled && !configuration.LoopTrigger;
                loopToggle.interactable = !configuration.Disabled;
                inputToggle.interactable = !configuration.Disabled;
            });
        }

        private void BindEnabled(RectTransform root, Func<bool> get, Action<bool> set)
        {
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var inputs = root.GetComponentsInChildren<TMP_InputField>(true);
            var dropdowns = root.GetComponentsInChildren<TMP_Dropdown>(true);
            BindSwitch(root, new Vector2(Padding, -Padding), get, set, enabled =>
            {
                group.alpha = enabled ? 1 : 0.45f;
                foreach (var input in inputs) input.interactable = enabled;
                foreach (var dropdown in dropdowns) dropdown.interactable = enabled;
            });
        }

        private Toggle BindSwitch(RectTransform root, Vector2 position, Func<bool> get, Action<bool> set,
            Action<bool> onRefresh = null)
        {
            var switchRoot = new GameObject("Switch", typeof(RectTransform), typeof(Image), typeof(Toggle));
            var rect = (RectTransform)switchRoot.transform;
            rect.SetParent(root, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
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
                onRefresh?.Invoke(enabled);
            }
            toggle.onValueChanged.AddListener(value =>
            {
                set(value);
                Refresh();
            });
            refreshers.Add(Refresh);
            Refresh();
            return toggle;
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

        private static CustomUIElement CreateLabel(RectTransform parent, Vector2 position, string key, float anchor = 0)
        {
            var root = CustomUIManager.Instance.SpawnElement(CustomUIManager.ElementKind.Label, parent, null, key);
            CustomUIMachineryStyle.Reskin(root);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(anchor, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(192, 36);
            var element = CustomUIElement.Bind(root, key);
            element.SetText(StorageTerminal.Localize("ui.io_bus." + key));
            return element;
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
