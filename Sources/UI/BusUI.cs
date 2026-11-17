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
            IOBus.InputOutputId => 200,
            TriggerBus.ItemId => 192,
            _ => 148
        };
        private int BaseRowHeight(object configuration) => configuration is TriggerBusManager.Configuration trigger
            && trigger.Options.Length > 0 ? DefaultRowHeight + 44 : DefaultRowHeight;
        private int RowHeight(object configuration) => BaseRowHeight(configuration) + (configuration switch
        {
            TriggerBusManager.Configuration trigger => trigger.Schedule.Count * 44,
            IOBusManager.Configuration io => (IOBus.CanInput(bus) ? io.InputSchedule.Count * 44 : 0)
                + (IOBus.CanOutput(bus) ? io.OutputSchedule.Count * 44 : 0),
            _ => 0
        });

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

        private void Rebuild(bool force = false)
        {
            var configurations = BusController.GetConfigurations(bus).ToArray();
            if (!force && rows.Select(row => row.Configuration).SequenceEqual(configurations)) return;
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
            ResizeWindow(resetScroll: !force);
        }

        private int ContentHeight(int count, bool includeSchedule = true) =>
            Padding * 2 + (count == 0 ? DefaultRowHeight : rows.Take(count).Sum(row => includeSchedule
                ? RowHeight(row.Configuration) : BaseRowHeight(row.Configuration)))
                + Mathf.Max(0, count - 1) * RowGap;

        private void ResizeWindow(bool resetScroll = true)
        {
            emptyState.SetActive(rows.Count == 0);
            int contentHeight = ContentHeight(rows.Count);
            int height = ContentHeight(Mathf.Min(rows.Count, VisibleRows), includeSchedule: false);
            bool overflow = contentHeight > height;
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
            scroll.content.sizeDelta = new Vector2(0, contentHeight);
            scroll.StopMovement();
            scroll.content.anchoredPosition = resetScroll ? Vector2.zero
                : new Vector2(0, Mathf.Clamp(scroll.content.anchoredPosition.y, 0, Mathf.Max(0, contentHeight - height)));
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
            var priorityLabel = CreateLabel(root, new Vector2(-Padding - 120 - Padding, -Padding),
                "storage_priority", anchor: 1);
            int priorityLabelWidth = FitLabel(priorityLabel);
            ((RectTransform)priorityLabel.root.transform).pivot = Vector2.one;
            BindText(CreateInput(root, new Vector2(Padding + 48, -Padding), -192 - priorityLabelWidth - Padding),
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
            int filterX = Padding * 2 + FitLabel(CreateLabel(root, new Vector2(Padding, -52), "storage_configuration"));
            BindFilter(CreateInput(root, new Vector2(filterX, -52), -Padding - filterX),
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
            BindText(CreateInput(root, new Vector2(Padding + 48, -Padding), -Padding * 2 - 48),
                () => configuration.Name, () => configuration.DefaultName, value => configuration.Name = value);
            string configurationKey = bus.identifier switch
            {
                IOBus.InputId => "input_configuration",
                IOBus.OutputId => "output_configuration",
                _ => "filter_configuration"
            };
            int filterX = Padding * 2 + FitLabel(CreateLabel(root, new Vector2(Padding, -52), configurationKey));
            BindFilter(CreateInput(root, new Vector2(filterX, -52), -Padding - filterX),
                () => configuration.Filter, () => configuration.DefaultFilter, value =>
                {
                    configuration.Filter = value;
                    configuration.Apply();
                });
            int y = 96;
            if (IOBus.CanInput(bus))
                y = BindSchedule(root, y, "input", configuration.InputSchedule);
            if (IOBus.CanOutput(bus))
                BindSchedule(root, y, "output", configuration.OutputSchedule);
            BindEnabled(root, () => !configuration.Disabled, value => configuration.Disabled = !value);
        }

        private void BuildTriggerRow(RectTransform root, TriggerBusManager.Configuration configuration)
        {
            var label = CreateLabel(root, new Vector2(Padding + 48, -Padding), "");
            ((RectTransform)label.root.transform).sizeDelta = new Vector2(Width * 2 - Padding * 4 - 48, 36);
            label.text.enableWordWrapping = false;
            label.text.overflowMode = TextOverflowModes.Ellipsis;
            label.text.richText = false;
            refreshers.Add(() => label.SetText(configuration.Name));
            label.SetText(configuration.Name);
            var inputToggle = BindToggle(root, new Vector2(Padding, -52), () => configuration.OnInputUpdate,
                value => configuration.OnInputUpdate = value,
                text: StorageTerminal.Localize("ui.trigger_bus.on_input_update"));
            var loopToggle = BindToggle(root, new Vector2(Padding, -96), () => configuration.LoopTrigger,
                value => configuration.LoopTrigger = value,
                text: StorageTerminal.Localize("ui.trigger_bus.loop_trigger"));
            int countX = 360 + Padding + FitLabel(CreateLabel(root, new Vector2(360, -96), "trigger_count"));
            var countInput = CreateInput(root, new Vector2(countX, -96), 128, stretch: false);
            BindInteger(countInput, () => configuration.TriggerCount, value =>
            {
                if (!configuration.LoopTrigger) configuration.Count = Mathf.Max(0, value);
            });
            var options = configuration.Options;
            if (options.Length > 0)
            {
                int optionX = Padding * 2 + FitLabel(CreateLabel(root, new Vector2(Padding, -140), "trigger_configuration"));
                var dropdownRoot = CustomUIManager.Instance.SpawnElement(
                    CustomUIManager.ElementKind.Dropdown, root, null, "TriggerOption");
                CustomUIMachineryStyle.Reskin(dropdownRoot);
                var rect = (RectTransform)dropdownRoot.transform;
                rect.anchorMin = rect.pivot = new Vector2(0, 1);
                rect.anchorMax = Vector2.one;
                rect.anchoredPosition = new Vector2(optionX, -140);
                rect.sizeDelta = new Vector2(-Padding - optionX, 36);
                var dropdown = CustomUIElement.Bind(dropdownRoot, "option").dropdown;
                dropdown.ClearOptions();
                dropdown.AddOptions(options.Select(option => option.Name).ToList());
                dropdown.onValueChanged.AddListener(index => configuration.OptionId = options[index].Id);
                void RefreshOption() => dropdown.SetValueWithoutNotify(
                    Array.FindIndex(options, option => option.Id == configuration.OptionId));
                refreshers.Add(RefreshOption);
                RefreshOption();
            }
            BindSchedule(root, options.Length > 0 ? 184 : 140, "trigger", configuration.Schedule);
            BindEnabled(root, () => !configuration.Disabled, value => configuration.Disabled = !value);
            refreshers.Add(() =>
            {
                countInput.interactable = !configuration.Disabled && !configuration.LoopTrigger;
                loopToggle.SetDisabled(configuration.Disabled);
                inputToggle.SetDisabled(configuration.Disabled);
            });
        }

        private void BindEnabled(RectTransform root, Func<bool> get, Action<bool> set)
        {
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var inputs = root.GetComponentsInChildren<TMP_InputField>(true);
            var dropdowns = root.GetComponentsInChildren<TMP_Dropdown>(true);
            BindToggle(root, new Vector2(Padding, -Padding), get, set, enabled =>
            {
                group.alpha = enabled ? 1 : 0.45f;
                foreach (var input in inputs) input.interactable = enabled;
                foreach (var dropdown in dropdowns) dropdown.interactable = enabled;
            });
        }

        private MachineryRadioButton BindToggle(RectTransform root, Vector2 position, Func<bool> get, Action<bool> set,
            Action<bool> onRefresh = null, string text = null)
        {
            var toggle = new MachineryRadioButton(text, get());
            toggle.handler.SetParent(host.handler);
            var rect = toggle.rectTransform;
            rect.SetParent(root, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position + new Vector2(0, -2);
            rect.sizeDelta = new Vector2(toggle.widthPixels, toggle.heightPixels);
            rect.localScale = new Vector3(2, 2, 1);

            void Refresh()
            {
                bool enabled = get();
                toggle.SetOn(enabled);
                onRefresh?.Invoke(enabled);
            }
            toggle.onChanged += value =>
            {
                set(value);
                Refresh();
            };
            refreshers.Add(Refresh);
            Refresh();
            return toggle;
        }

        private int BindSchedule(RectTransform root, int y, string direction, List<BusScheduleEntry> schedule)
        {
            int height = (schedule.Count + 1) * 44;
            var table = new GameObject("ScheduleTable", typeof(RectTransform)).GetComponent<RectTransform>();
            table.SetParent(root, false);
            table.anchorMin = table.pivot = new Vector2(0, 1);
            table.anchorMax = Vector2.one;
            table.anchoredPosition = new Vector2(Padding, -y);
            table.sizeDelta = new Vector2(-Padding * 2, height);
            CreateTableLine(table, true, 0);
            CreateTableLine(table, true, 64);
            CreateTableLine(table, true, 0, anchor: 0.5f);
            CreateTableLine(table, true, -64, anchor: 1);
            CreateTableLine(table, true, -2, anchor: 1);
            for (int line = 0; line <= schedule.Count; line++) CreateTableLine(table, false, line * 44);
            CreateTableLine(table, false, height - 2);
            for (int column = 1; column <= 2; column++)
            {
                var label = CreateLabel(CreateScheduleCell(table, 0, column), new Vector2(4, -4),
                    direction + (column == 1 ? "_order" : "_priority"));
                var rect = (RectTransform)label.root.transform;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = new Vector2(-8, 36);
                label.text.alignment = TextAlignmentOptions.Center;
            }
            CreateScheduleButton(CreateScheduleCell(table, 0, 3), 4, "+", () =>
            {
                schedule.Add(new BusScheduleEntry());
                Rebuild(force: true);
            });
            int row = 1;
            foreach (var entry in schedule)
            {
                BindToggle(CreateScheduleCell(table, row, 0), new Vector2(16, -4),
                    () => entry.Enabled, value => entry.Enabled = value);
                BindInteger(CreateInput(CreateScheduleCell(table, row, 1), new Vector2(4, -4), -8),
                    () => entry.Order, value => entry.Order = Mathf.Clamp(value, 1, 15));
                BindInteger(CreateInput(CreateScheduleCell(table, row, 2), new Vector2(4, -4), -8),
                    () => entry.Priority, value => entry.Priority = value);
                CreateScheduleButton(CreateScheduleCell(table, row, 3), 4, "×", () =>
                {
                    schedule.Remove(entry);
                    Rebuild(force: true);
                });
                row++;
            }
            return y + height + RowGap;
        }

        private static RectTransform CreateScheduleCell(RectTransform table, int row, int column)
        {
            var rect = new GameObject("ScheduleCell", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(table, false);
            var (min, max, x, width) = column switch
            {
                0 => (0f, 0f, 0, 64),
                1 => (0f, 0.5f, 64, -64),
                2 => (0.5f, 1f, 0, -64),
                _ => (1f, 1f, -64, 64)
            };
            rect.anchorMin = new Vector2(min, 1);
            rect.anchorMax = new Vector2(max, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -row * 44);
            rect.sizeDelta = new Vector2(width, 44);
            return rect;
        }

        private static void CreateTableLine(RectTransform parent, bool vertical, int position, float anchor = 0)
        {
            var root = new GameObject("TableBorder", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)root.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = vertical ? new Vector2(anchor, 0) : new Vector2(0, 1);
            rect.anchorMax = vertical ? new Vector2(anchor, 1) : Vector2.one;
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = vertical ? new Vector2(position, 0) : new Vector2(0, -position);
            rect.sizeDelta = vertical ? new Vector2(2, 0) : new Vector2(0, 2);
            var image = root.GetComponent<Image>();
            image.color = new Color(0.55f, 0.55f, 0.55f, 1);
            image.raycastTarget = false;
        }

        private static void CreateScheduleButton(RectTransform parent, int y, string text, Action onClick)
        {
            var root = CustomUIManager.Instance.SpawnElement(CustomUIManager.ElementKind.Button, parent, null, "ScheduleButton");
            CustomUIMachineryStyle.Reskin(root);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-Padding, -y);
            rect.sizeDelta = new Vector2(48, 36);
            var element = CustomUIElement.Bind(root, "schedule");
            element.SetText(text);
            element.button.onClick.AddListener(() => onClick());
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

        private static int FitLabel(CustomUIElement label)
        {
            label.text.enableAutoSizing = false;
            label.text.enableWordWrapping = false;
            int width = Mathf.CeilToInt(label.text.GetPreferredValues(label.text.text).x);
            ((RectTransform)label.root.transform).sizeDelta = new Vector2(width, 36);
            return width;
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
