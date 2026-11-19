using System;
using TMPro;
using UnityEngine.UI;
using DigitalLogistics.Filtering;
using DigitalLogistics.UI;

namespace DigitalLogistics
{
    internal static class DebugMenu
    {
        private const string WindowId = "digital_logistics__debug";
        private const string ProvidersWindowId = "digital_logistics__providers_debug";
        private const string SearchQueryWindowId = "digital_logistics__search_query_debug";
        private const string ExpressionWindowId = "digital_logistics__expression_debug";
        private const string PerformanceWindowId = "digital_logistics__performance_debug";

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

            var builder = CreateWindow(WindowId, "DigitalLogistics", CustomUIDebugPanel.WINDOW_ID);
            AddMenuButton(builder, "Mod save data", () => ModDataDebugView.Open(WindowId));
            AddMenuButton(builder, "View storage providers", OpenProviders);
            AddMenuButton(builder, "Parse search query", OpenSearchQuery);
            AddMenuButton(builder, "Parse expression", OpenExpression);
            AddMenuButton(builder, "Performance trace", OpenPerformance);
            CustomUIDebugPanel.SetBackgroundAlpha(builder.Show(), 0.8f);
        }

        private static void AddMenuButton(CustomUIBuilder builder, string text, Action onClick)
        {
            builder.AddButton(text, onClick);
            builder.Last.SetPreferredSize(-1, 48).SetFlexible(1, 0);
            builder.Last.root.GetComponent<LayoutElement>().minHeight = 48;
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
            foreach (var provider in DigitalLogistics.Manager.Providers)
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
            _ = new SearchQueryDebugView(builder, StorageTerminalSearchParser.Parse, "Search query");
            CustomUIDebugPanel.SetBackgroundAlpha(builder.Show(), 0.8f);
        }

        private static void OpenExpression()
        {
            var builder = CreateWindow(ExpressionWindowId, "Parse expression", WindowId);
            _ = new SearchQueryDebugView(builder, CachedItemExpressionParser.Parse, "Expression");
            CustomUIDebugPanel.SetBackgroundAlpha(builder.Show(), 0.8f);
        }

        private static void OpenPerformance()
        {
            var builder = CreateWindow(PerformanceWindowId, "Performance trace", WindowId);
            _ = new PerformanceTraceView(builder);
            CustomUIDebugPanel.SetBackgroundAlpha(builder.Show(), 0.8f);
        }

        internal static void Close()
        {
            ModDataDebugView.Close();
            CustomUIManager.Instance?.CloseWindow(PerformanceWindowId);
            CustomUIManager.Instance?.CloseWindow(ExpressionWindowId);
            CustomUIManager.Instance?.CloseWindow(SearchQueryWindowId);
            CustomUIManager.Instance?.CloseWindow(ProvidersWindowId);
            CustomUIManager.Instance?.CloseWindow(WindowId);
        }

        internal static CustomUIBuilder CreateWindow(string id, string title, string parentId)
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
