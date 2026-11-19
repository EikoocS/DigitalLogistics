using System;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Buses;
using DigitalLogistics.Item;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static DigitalLogistics.UI.Common.MachineryControlFactory;
using static DigitalLogistics.UI.Buses.BusRowControls;

namespace DigitalLogistics.UI.Buses
{
    internal sealed class BusUI : MonoBehaviour
    {
        private const int VisibleRows = 4;

        private sealed class Row
        {
            internal IBusConfigurationRow Editor;
            internal BusRowControls Controls;
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
        private readonly List<Action> globalRefreshers = new();
        private bool IsIO => IOBus.IsIO(bus) || FluidIOBus.IsIO(bus);
        private int EmptyRowHeight => bus.identifier switch
        {
            StorageBus.ItemId or FluidStorageBus.ItemId => StorageBusRow.DefaultHeight,
            TriggerBus.ItemId => TriggerBusRow.DefaultHeight,
            _ => FluidIOBus.IsIO(bus) ? FluidIOBusRow.GetBaseHeight(bus) : IOBusRow.GetBaseHeight(bus)
        };

        internal static PixelWindow Create(GameItem bus)
        {
            var (host, root) = CreateControl(CustomUIManager.ElementKind.ScrollView, Width, 56, canResize: true);
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
                var onAdd = ui.CreateToggle(FluidIOBus.IsIO(bus) ? "ui.fluid_bus.on_add" : "ui.io_bus.on_add", IOBusManager.OnAddTag);
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
            foreach (var row in rows) row.Controls.Refresh();
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
            label.SetText(DigitalLogistics.Localize("ui.bus.no_connections"));
            label.text.alignment = TextAlignmentOptions.Center;
            label.text.enableAutoSizing = false;
            label.text.richText = false;
            label.text.raycastTarget = false;
            return root;
        }

        private MachineryRadioButton CreateToggle(string key, string tag)
        {
            var toggle = new MachineryRadioButton(DigitalLogistics.Localize(key),
                bus.GetTagReadonly(tag).GetBool(), value =>
                    bus.ModifyTag(tag, state => state.Enable().SetBool(value)));
            globalRefreshers.Add(() => toggle.SetOn(bus.GetTagReadonly(tag).GetBool()));
            return toggle;
        }

        internal static void RefreshConnections(GameItem bus) =>
            bus.contentWindow.handler.GetComponentInChildren<BusUI>(true).Rebuild();

        private void Rebuild(bool force = false)
        {
            var connections = BusController.GetConfigurations(bus).ToArray();
            if (!force && rows.Select(row => row.Editor.Connection).SequenceEqual(connections)) return;
            foreach (var row in rows) row.Controls.Destroy();
            rows.Clear();
            int y = Padding;
            foreach (var connection in connections)
            {
                IBusConfigurationRow editor = connection switch
                {
                    StorageBusManager.Connection storage => new StorageBusRow(storage),
                    FluidStorageBusManager.Connection storage => new StorageBusRow(storage),
                    FluidIOBusManager.Connection fluid => new FluidIOBusRow(fluid),
                    TriggerBusManager.Connection trigger => new TriggerBusRow(trigger),
                    _ => new IOBusRow((IOBusManager.Connection)connection)
                };
                var rowControls = new BusRowControls(scroll.content, host.handler, y, editor.Height,
                    () => Rebuild(force: true));
                editor.Build(rowControls);
                rows.Add(new Row { Editor = editor, Controls = rowControls });
                y += editor.Height + RowGap;
            }
            ResizeWindow(resetScroll: !force);
        }

        private int ContentHeight(int count, bool includeSchedule = true) =>
            Padding * 2 + (count == 0 ? EmptyRowHeight : rows.Take(count).Sum(row => includeSchedule
                ? row.Editor.Height : row.Editor.BaseHeight))
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
    }
}
