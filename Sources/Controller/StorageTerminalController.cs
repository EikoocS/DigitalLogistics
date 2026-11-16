using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DigitalStorage.Filtering;
using DigitalStorage.Storage;
using DigitalStorage.UI;
using UnityEngine;

namespace DigitalStorage.Controller
{
    internal sealed class StorageTerminalController : MonoBehaviour
    {
        private const double RefreshBudgetSeconds = 0.002;
        private const double SearchDelaySeconds = 0.3;

        private sealed class GroupState
        {
            internal StoredItem Item;
            internal StorageTerminalGroup Group;
            internal StorageTerminalList.GroupView View;
            internal StorageTerminalRowController Header;
            internal bool Expanded;
            internal bool Queued;
            internal int RenderedItems;
            internal readonly Dictionary<int, StorageTerminalRowController> Rows = new();
        }

        private StorageTerminalUI view;
        private StorageTerminalList List => view.List;
        private readonly Dictionary<string, GroupState> groups = new();
        private readonly Queue<GroupState> pendingChildren = new Queue<GroupState>();
        private CancellationTokenSource searchCancellation;
        private TaskCompletionSource<bool> nextFrame;
        private double searchDeadline = double.PositiveInfinity;
        private bool inventoryChanged;
        private readonly HashSet<string> expandedGroups = new();
        private List<StoredItem> pendingItems;

        internal void Init(StorageTerminalUI view)
        {
            this.view = view;
            view.SearchChanged += OnSearchChanged;
            view.Configuration.Changed += OnConfigurationChanged;
            DigitalStorage.Manager.Changed += OnInventoryChanged;
        }

        internal void Refresh()
        {
            DigitalStorage.Manager.Refresh();
            view.Configuration.RefreshDescriptions();
            Refresh(false);
        }

        private async void Refresh(bool incremental)
        {
            StopSearch();
            if (!isActiveAndEnabled) return;

            inventoryChanged = false;
            pendingItems = null;
            if (!incremental)
            {
                expandedGroups.Clear();
                foreach (var group in groups.Values) group.View.Destroy();
                groups.Clear();
                pendingChildren.Clear();
                List.ResetScroll();
            }
            List.RefreshPriceUnit();

            var expression = StorageTerminalSearchParser.Parse(view.SearchText);
            if (view.Configuration.Safety != StorageTerminalConfiguration.SafetyFilter.All)
            {
                var unsafeItems = StoredItemExpression.ContrabandLevel.EqualTo(0).Not()
                    .Or(StoredItemExpression.Heat.EqualTo(0).Not());
                expression = expression.And(view.Configuration.Safety == StorageTerminalConfiguration.SafetyFilter.Legal
                    ? unsafeItems.Not() : unsafeItems);
            }
            var items = incremental ? new List<StoredItem>() : null;
            var cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            searchCancellation = cancellation;

            try
            {
                await using var enumerator = DigitalStorage.Manager.SearchAsync(expression,
                    view.Configuration.SortBy, view.Configuration.Reverse, cancellationToken: token).GetAsyncEnumerator();
                while (!token.IsCancellationRequested)
                {
                    double deadline = Time.realtimeSinceStartupAsDouble + RefreshBudgetSeconds;
                    bool finished = false;

                    while (Time.realtimeSinceStartupAsDouble < deadline)
                    {
                        bool hasItem = await enumerator.MoveNextAsync();
                        token.ThrowIfCancellationRequested();
                        if (!hasItem)
                        {
                            finished = true;
                            break;
                        }
                        if (incremental) items.Add(enumerator.Current);
                        else AddItem(enumerator.Current);
                    }

                    if (finished)
                    {
                        pendingItems = items;
                        return;
                    }
                    await WaitForNextFrame();
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                if (ReferenceEquals(searchCancellation, cancellation)) searchCancellation = null;
                cancellation.Dispose();
            }
        }

        private void ApplyChanges(List<StoredItem> items)
        {
            var results = new Dictionary<string, List<StoredItem>>();
            var order = new List<string>();
            foreach (var item in items)
            {
                if (!results.TryGetValue(item.DisplayName, out var group))
                {
                    results.Add(item.DisplayName, group = new List<StoredItem>());
                    order.Add(item.DisplayName);
                }
                group.Add(item);
            }

            pendingChildren.Clear();
            foreach (var entry in groups.ToArray())
            {
                entry.Value.Queued = false;
                if (results.ContainsKey(entry.Key)) continue;
                entry.Value.View.Destroy();
                groups.Remove(entry.Key);
                expandedGroups.Remove(entry.Key);
            }

            int index = 0;
            foreach (string name in order)
            {
                var result = results[name];
                if (!groups.TryGetValue(name, out var state))
                {
                    foreach (var item in result) AddItem(item);
                    state = groups[name];
                }
                else if (result.Count == 1) UpdateSingle(state, result[0]);
                else UpdateGroup(state, name, result);

                state.View.SetIndex(index);
                index++;
                QueueChildren(state);
            }
        }

        private void UpdateSingle(GroupState state, StoredItem item)
        {
            if (state.Group != null)
            {
                state.View.MakeSingle();
                state.Group = null;
                state.Rows.Clear();
                state.Expanded = false;
                state.RenderedItems = 0;
            }
            state.Item = item;
            UpdateRow(state.Header, item);
        }

        private void UpdateGroup(GroupState state, string name, List<StoredItem> items)
        {
            if (state.Group == null) CreateGroup(state, name);
            bool orderChanged = !state.Group.Items.Select(item => item.UniqueId)
                .SequenceEqual(items.Select(item => item.UniqueId));
            var remaining = new HashSet<int>(items.Select(item => item.UniqueId));
            foreach (var entry in state.Rows.ToArray())
            {
                if (remaining.Contains(entry.Key)) continue;
                entry.Value.View.Destroy();
                state.Rows.Remove(entry.Key);
            }

            state.Group.SetItems(items);
            for (int i = 0; i < items.Count; i++)
            {
                if (!state.Rows.TryGetValue(items[i].UniqueId, out var row)) continue;
                UpdateRow(row, items[i]);
                row.View.SetIndex(i);
            }
            if (orderChanged) state.RenderedItems = 0;
            UpdateSummary(state);
        }

        private void AddItem(StoredItem item)
        {
            string name = item.DisplayName;
            if (!groups.TryGetValue(name, out var state))
            {
                var row = new StorageTerminalRowController(this, item);
                var entry = List.CreateRow(row);
                row.View = entry.Header;
                UpdateRow(row, item);
                groups.Add(name, new GroupState { Item = item, View = entry, Header = row });
                return;
            }
            if (state.Group == null) CreateGroup(state, name);
            state.Group.Add(item);
            UpdateSummary(state);
            QueueChildren(state);
        }

        private void CreateGroup(GroupState state, string name)
        {
            var header = state.Header;
            header.Item = null;
            state.Expanded = expandedGroups.Contains(name);
            state.Group = new StorageTerminalGroup(name);
            header.Group = state.Group;
            state.Group.Add(state.Item);
            state.Item = null;
            state.View.MakeGroup(state.Expanded, () => ToggleGroup(state, name));
            UpdateSummary(state);
        }

        private void ToggleGroup(GroupState state, string name)
        {
            state.Expanded = !state.Expanded;
            if (state.Expanded) expandedGroups.Add(name);
            else expandedGroups.Remove(name);
            state.View.SetExpanded(state.Expanded);
            UpdateSummary(state);
            QueueChildren(state);
        }

        private void UpdateSummary(GroupState state) =>
            List.ShowRow(state.Header.View, state.Group.Name, state.Group.ContrabandLevel,
                state.Group.Heat, state.Group.TotalPrice, state.Group.Items.Count);

        private void QueueChildren(GroupState state)
        {
            if (state.Group == null || !state.Expanded || state.Queued
                || state.RenderedItems == state.Group.Items.Count) return;
            state.Queued = true;
            pendingChildren.Enqueue(state);
        }

        private void RenderChildren(double deadline)
        {
            while (pendingChildren.Count > 0 && Time.realtimeSinceStartupAsDouble < deadline)
            {
                var state = pendingChildren.Dequeue();
                state.Queued = false;
                if (!state.Expanded) continue;
                int index = state.RenderedItems++;
                var item = state.Group.Items[index];
                if (!state.Rows.TryGetValue(item.UniqueId, out var row))
                {
                    row = new StorageTerminalRowController(this, item);
                    row.View = state.View.CreateRow(row);
                    UpdateRow(row, item);
                    state.Rows.Add(item.UniqueId, row);
                }
                row.View.SetIndex(index);
                QueueChildren(state);
            }
        }

        private void UpdateRow(StorageTerminalRowController row, StoredItem item)
        {
            row.Item = item;
            row.Group = null;
            List.ShowRow(row.View, item.DisplayName, item.ContrabandLevel, item.Heat, item.Price);
        }

        private Task WaitForNextFrame()
        {
            nextFrame = new TaskCompletionSource<bool>();
            return nextFrame.Task;
        }

        private void ResumeFrame()
        {
            var pending = nextFrame;
            nextFrame = null;
            pending?.TrySetResult(true);
        }

        private void StopSearch()
        {
            searchDeadline = double.PositiveInfinity;
            searchCancellation?.Cancel();
            ResumeFrame();
        }

        private void OnSearchChanged(string text)
        {
            StopSearch();
            searchDeadline = Time.realtimeSinceStartupAsDouble + SearchDelaySeconds;
        }

        private void OnConfigurationChanged() => Refresh(false);

        private void LateUpdate()
        {
            if (Time.realtimeSinceStartupAsDouble >= searchDeadline) Refresh(false);
            else if (inventoryChanged && double.IsPositiveInfinity(searchDeadline))
                Refresh(true);
            if (pendingItems != null)
            {
                var items = pendingItems;
                pendingItems = null;
                ApplyChanges(items);
            }
            double deadline = Time.realtimeSinceStartupAsDouble + RefreshBudgetSeconds;
            RenderChildren(deadline);
            ResumeFrame();
        }

        private void OnInventoryChanged()
        {
            inventoryChanged = true;
            pendingItems = null;
            searchCancellation?.Cancel();
            ResumeFrame();
        }

        private void OnDisable()
        {
            StopSearch();
        }

        private void OnDestroy()
        {
            view.SearchChanged -= OnSearchChanged;
            view.Configuration.Changed -= OnConfigurationChanged;
            DigitalStorage.Manager.Changed -= OnInventoryChanged;
            view.Dispose();
        }
    }
}
