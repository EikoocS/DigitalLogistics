using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using DigitalStorage.Storage;

namespace DigitalStorage
{
    internal static class DebugMenu
    {
        private const string WindowId = "digital_storage__debug";
        private const string ProvidersWindowId = "digital_storage__providers_debug";
        private const string SearchQueryWindowId = "digital_storage__search_query_debug";

        internal static void Open()
        {
            var manager = CustomUIManager.Instance;
            var window = manager.GetWindow(WindowId);
            if (window != null && window.IsAlive)
            {
                var parent = manager.GetWindow(CustomUIDebugPanel.WINDOW_ID);
                CopyLayout(parent, window);
                parent.Hide();
                window.Show();
                return;
            }

            var builder = CreateWindow(WindowId, "DigitalStorage", CustomUIDebugPanel.WINDOW_ID);
            builder.AddButton("View storage providers", OpenProviders);
            builder.AddButton("Parse search query", OpenSearchQuery);
            CustomUIDebugPanel.SetBackgroundAlpha(builder.Show(), 0.8f);
        }

        private static void OpenProviders()
        {
            var builder = CreateWindow(ProvidersWindowId, "View storage providers", WindowId);
            CustomUIBuilder list = null;
            builder.AddButton("Refresh", () => RefreshProviders(list));
            builder.BeginScrollFlexible();
            list = new CustomUIBuilder(CustomUIManager.Instance, builder.Handle, builder.CurrentContainer);
            builder.End();
            RefreshProviders(list);
            CustomUIDebugPanel.SetBackgroundAlpha(builder.Show(), 0.8f);
        }

        private static void RefreshProviders(CustomUIBuilder builder)
        {
            CustomUIBuilder.ClearContainer(builder.CurrentContainer);
            foreach (var provider in DigitalStorage.Manager.Providers)
            {
                builder.AddLabel(provider.Name);
                builder.Last.text.richText = false;
                builder.Last.text.enableWordWrapping = false;
                builder.Last.text.overflowMode = TextOverflowModes.Ellipsis;
            }
            CustomUIBuilder.RebuildLayout(builder.CurrentContainer);
        }

        private static void OpenSearchQuery()
        {
            var builder = CreateWindow(SearchQueryWindowId, "Parse search query", WindowId);
            CustomUIBuilder output = null;
            builder.AddInput("Search query", onTyping: text => RefreshSearchQuery(output, text));
            builder.Last.input.lineType = TMP_InputField.LineType.SingleLine;
            builder.BeginScrollFlexible();
            output = new CustomUIBuilder(CustomUIManager.Instance, builder.Handle, builder.CurrentContainer);
            builder.End();
            RefreshSearchQuery(output, "");
            CustomUIDebugPanel.SetBackgroundAlpha(builder.Show(), 0.8f);
        }

        private static void RefreshSearchQuery(CustomUIBuilder builder, string input)
        {
            var query = new StorageQuery(input);
            var lines = new[]
            {
                $"sort: {query.sort}",
                $"descending: {query.descending}",
                $"priceFilters: {FormatValues(query.priceFilters.ConvertAll(filter => $"{filter.Comparison} {filter.Value}"))}",
                $"requiredNames: {FormatValues(query.requiredNames)}",
                $"anyNames: {FormatValues(query.anyNames)}",
                $"excludedNames: {FormatValues(query.excludedNames)}",
                $"requiredMask: {FormatTags(query.requiredMask)}",
                $"anyMask: {FormatTags(query.anyMask)}",
                $"excludedMask: {FormatTags(query.excludedMask)}"
            };
            CustomUIBuilder.ClearContainer(builder.CurrentContainer);
            foreach (var line in lines)
            {
                builder.AddLabel(line);
                var label = builder.Last;
                label.text.richText = false;
                label.text.enableWordWrapping = true;
                label.text.overflowMode = TextOverflowModes.Overflow;
                label.text.alignment = TextAlignmentOptions.TopLeft;
                var layout = label.root.GetComponent<LayoutElement>();
                if (layout != null)
                {
                    layout.minHeight = -1;
                    layout.preferredHeight = -1;
                    layout.flexibleHeight = 0;
                }
            }
            CustomUIBuilder.RebuildLayout(builder.CurrentContainer);
        }

        private static string FormatTags(ulong mask) =>
            FormatValues(StorageItemTypeHelper.ToTags(mask).ConvertAll(StorageItemTypeHelper.GetLocalizedName));

        private static string FormatValues(IEnumerable<string> values)
        {
            var quoted = new List<string>();
            foreach (var value in values)
                quoted.Add("\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"");
            return "[" + string.Join(", ", quoted) + "]";
        }

        internal static void Close()
        {
            CustomUIManager.Instance?.CloseWindow(SearchQueryWindowId);
            CustomUIManager.Instance?.CloseWindow(ProvidersWindowId);
            CustomUIManager.Instance?.CloseWindow(WindowId);
        }

        private static CustomUIBuilder CreateWindow(string id, string title, string parentId)
        {
            var manager = CustomUIManager.Instance;
            var builder = manager.CreateWindow(id, title, "rear")
                .SetDraggable(true)
                .SetCloseOnEscape();
            var window = builder.Handle;
            var parent = manager.GetWindow(parentId);
            CopyLayout(parent, window);
            parent.Hide();
            builder.OnClose(() =>
            {
                var previous = manager.GetWindow(parentId);
                if (previous != null)
                {
                    CopyLayout(window, previous);
                    previous.Show();
                }
            });
            return builder;
        }

        private static void CopyLayout(CustomUIWindow source, CustomUIWindow target)
        {
            var size = source.Rect.sizeDelta;
            target.SetSize(size.x, size.y);
            target.SetPosition(source.Rect.anchoredPosition);
            var sourceLayout = source.root.GetComponent<VerticalLayoutGroup>();
            var targetLayout = target.root.GetComponent<VerticalLayoutGroup>();
            if (sourceLayout != null && targetLayout != null)
                targetLayout.padding = sourceLayout.padding;
        }
    }
}
