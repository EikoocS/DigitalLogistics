using System;
using System.Linq;
using DigitalLogistics.Tracing;
using TMPro;
using UnityEngine.UI;

namespace DigitalLogistics.UI
{
    internal sealed class PerformanceTraceView
    {
        private readonly CustomUIBuilder output;
        private readonly CustomUIElement start;
        private readonly CustomUIElement stop;

        internal PerformanceTraceView(CustomUIBuilder builder)
        {
            builder.BeginRow();
            start = AddButton("Start new", PerformanceTrace.Start);
            stop = AddButton("Stop", PerformanceTrace.Stop);
            AddButton("Clear", PerformanceTrace.Clear);
            AddButton("Refresh", null);
            builder.End();

            builder.BeginScrollFlexible();
            output = new CustomUIBuilder(CustomUIManager.Instance, builder.Handle, builder.CurrentContainer);
            builder.End();
            Refresh();

            CustomUIElement AddButton(string title, Action action)
            {
                builder.AddButton(title, () =>
                {
                    action?.Invoke();
                    Refresh();
                });
                return builder.Last.SetPreferredSize(-1, 36).SetFlexible(1, 0);
            }
        }

        private void Refresh()
        {
            var trace = PerformanceTrace.Latest;
            bool recording = trace?.Recording == true;
            start.button.interactable = !recording;
            stop.button.interactable = recording;

            CustomUIBuilder.ClearContainer(output.CurrentContainer);
            AddLabel(recording ? "Recording" : trace != null ? "Stopped" : "Idle");
            if (trace != null)
            {
                output.AddHeader("Drive connections");
                var drives = trace.DriveConnections;
                AddLabel(drives.Count == 0 ? "—" : $"{drives.Count:N0}\n{FormatTiming(drives)}");

                output.AddHeader("Bus batches");
                foreach (var entry in trace.BusBatches.OrderBy(entry => entry.Key))
                    AddLabel($"{entry.Key} · {entry.Value.Count:N0}\n{FormatTiming(entry.Value)}");
                if (trace.BusBatches.Count == 0) AddLabel("—");

                output.AddHeader("Bus operations");
                foreach (var entry in trace.BusOperations.OrderByDescending(entry => entry.Value.Time.MaxTicks))
                {
                    var (timing, busId) = entry.Value;
                    string slowest = busId == 0 ? "" : $" (#{busId})";
                    AddLabel($"{entry.Key.Type} / {entry.Key.Kind} · {timing.Count:N0}\n{FormatTiming(timing)}{slowest}");
                }
                if (trace.BusOperations.Count == 0) AddLabel("—");

                output.AddHeader("Terminal searches");
                foreach (var entry in trace.Searches.OrderBy(entry => entry.Key))
                {
                    var (lookup, list, response, results) = entry.Value;
                    AddLabel($"{entry.Key} · {response.Count:N0} · {results:N0} matched stacks\n"
                        + $"Lookup: {FormatTiming(lookup)}\n"
                        + $"List: {FormatTiming(list)}\n"
                        + $"Elapsed: {FormatTiming(response)}");
                }
                if (trace.Searches.Count == 0) AddLabel("—");

                output.AddHeader("Disks written (latest per disk)");
                foreach (var entry in trace.Disks.OrderByDescending(entry => entry.Value.Size.Base64Bytes))
                {
                    var (size, ticks) = entry.Value;
                    AddLabel($"{entry.Key:D}\n"
                        + $"JSON {size.JsonBytes:N0} B · encode {Milliseconds(ticks)}\n"
                        + $"GZip {size.GZipBytes:N0} B · {size.GZipSavings:P1} saved\n"
                        + $"Base64 {size.Base64Bytes:N0} B · {size.Base64Savings:P1} saved");
                }
                if (trace.Disks.Count == 0) AddLabel("—");
            }
            CustomUIBuilder.RebuildLayout(output.CurrentContainer);
        }

        private void AddLabel(string text)
        {
            output.AddLabel(text);
            var label = output.Last;
            label.text.richText = false;
            label.text.enableAutoSizing = false;
            label.text.enableWordWrapping = true;
            label.text.alignment = TextAlignmentOptions.TopLeft;
            var layout = label.root.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.minHeight = layout.preferredHeight = -1;
                layout.flexibleHeight = 0;
            }
        }

        private static string FormatTiming(TimingStats stats) =>
            $"Total {Milliseconds(stats.TotalTicks)} · avg {Milliseconds(stats.AverageTicks)} · max {Milliseconds(stats.MaxTicks)}";

        private static string Milliseconds(double ticks) => $"{TraceTimer.Milliseconds(ticks):F3} ms";
    }
}
