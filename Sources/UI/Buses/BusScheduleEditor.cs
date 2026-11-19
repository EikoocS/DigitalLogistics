using System;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Buses;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static DigitalLogistics.UI.Common.MachineryControlFactory;
using static DigitalLogistics.UI.Buses.BusRowControls;

namespace DigitalLogistics.UI.Buses
{
    internal static class BusScheduleEditor
    {
        internal const int EntryHeight = 44;

        internal static int Build<T>(BusRowControls controls, int y, string direction, List<T> schedule, Action rebuild,
            Action<RectTransform, T> bindFilter = null, Action<RectTransform, T> bindAmount = null,
            string amountHeader = "schedule_count", Action<RectTransform, T, RectTransform> bindUnlimited = null)
            where T : BusScheduleEntry, new()
        {
            var root = controls.Root;
            const int edge = 64;
            var divisions = bindUnlimited != null
                ? bindAmount != null ? new[] { 0f, 0.14f, 0.28f, 0.40f, 0.76f, 1f }
                    : new[] { 0f, 0.18f, 0.36f, 0.48f, 0.64f, 1f }
                : bindAmount != null ? new[] { 0f, 0.15f, 0.30f, 0.65f, 1f }
                : bindFilter == null ? new[] { 0f, 1f / 3, 2f / 3, 1f }
                : new[] { 0f, 0.22f, 0.44f, 0.62f, 1f };
            var boundaries = new List<(float Anchor, float Offset)> { (0, 0) };
            boundaries.AddRange(divisions.Select(part => (part, edge * (1 - 2 * part))));
            boundaries.Add((1, 0));
            int lastColumn = boundaries.Count - 2;
            int height = (schedule.Count + 1) * EntryHeight;
            var table = new GameObject("ScheduleTable", typeof(RectTransform)).GetComponent<RectTransform>();
            table.SetParent(root, false);
            table.anchorMin = table.pivot = new Vector2(0, 1);
            table.anchorMax = Vector2.one;
            table.anchoredPosition = new Vector2(Padding, -y);
            table.sizeDelta = new Vector2(-Padding * 2, height);
            foreach (var (anchor, offset) in boundaries.Take(boundaries.Count - 1))
                CreateTableLine(table, true, offset, anchor);
            CreateTableLine(table, true, -2, anchor: 1);
            for (int line = 0; line <= schedule.Count; line++) CreateTableLine(table, false, line * EntryHeight);
            CreateTableLine(table, false, height - 2);

            RectTransform Cell(int row, int column) =>
                CreateScheduleCell(table, row, boundaries[column], boundaries[column + 1]);
            void Header(int column, string key)
            {
                var label = CreateLabel(Cell(0, column), new Vector2(4, -4), DigitalLogistics.Localize("ui.io_bus." + key));
                var rect = (RectTransform)label.root.transform;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = new Vector2(-8, 36);
                label.text.alignment = TextAlignmentOptions.Center;
            }
            Header(1, direction + "_order");
            Header(2, direction + "_priority");
            int amountColumn = bindUnlimited == null ? 3 : 4;
            if (bindUnlimited != null) Header(3, "schedule_unlimited");
            Header(amountColumn, amountHeader);
            if (bindFilter != null) Header(amountColumn + 1, "schedule_filter");
            CreateButton(Cell(0, lastColumn), new Vector2(-Padding, -4), new Vector2(48, 36), "+", () =>
            {
                schedule.Add(new T());
                rebuild();
            });
            int row = 1;
            foreach (var entry in schedule)
            {
                controls.BindToggle(Cell(row, 0), new Vector2(16, -4), () => entry.Enabled, value => entry.Enabled = value);
                controls.BindInteger(CreateInput(Cell(row, 1), new Vector2(4, -4), -8),
                    () => entry.Order, value => entry.Order = Mathf.Clamp(value, 1, 15));
                controls.BindInteger(CreateInput(Cell(row, 2), new Vector2(4, -4), -8),
                    () => entry.Priority, value => entry.Priority = value);
                var amountCell = Cell(row, amountColumn);
                if (bindAmount != null) bindAmount(amountCell, entry);
                else
                {
                    var counted = (CountedBusScheduleEntry)(BusScheduleEntry)entry;
                    controls.BindInteger(CreateInput(amountCell, new Vector2(4, -4), -8),
                        () => counted.RepeatCount, value => counted.Count = Mathf.Max(1, value));
                }
                if (bindFilter != null) bindFilter(Cell(row, amountColumn + 1), entry);
                if (bindUnlimited != null) bindUnlimited(Cell(row, 3), entry, amountCell);
                CreateButton(Cell(row, lastColumn), new Vector2(-Padding, -4), new Vector2(48, 36), "×", () =>
                {
                    schedule.Remove(entry);
                    rebuild();
                });
                row++;
            }
            return y + height + RowGap;
        }

        private static RectTransform CreateScheduleCell(RectTransform table, int row,
            (float Anchor, float Offset) left, (float Anchor, float Offset) right)
        {
            var rect = new GameObject("ScheduleCell", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(table, false);
            rect.anchorMin = new Vector2(left.Anchor, 1);
            rect.anchorMax = new Vector2(right.Anchor, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(left.Offset, -row * EntryHeight);
            rect.sizeDelta = new Vector2(right.Offset - left.Offset, EntryHeight);
            return rect;
        }

        private static void CreateTableLine(RectTransform parent, bool vertical, float position, float anchor = 0)
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
    }
}
