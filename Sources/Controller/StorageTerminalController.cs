using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DigitalLogistics.Collections;
using DigitalLogistics.Filtering;
using DigitalLogistics.Storage;
using DigitalLogistics.UI;
using UnityEngine;

namespace DigitalLogistics.Controller
{
    internal sealed class StorageTerminalController : MonoBehaviour
    {
        private static int activeTerminals;
        internal static bool HasActiveTerminal => activeTerminals > 0;
        private static double RefreshBudgetSeconds => DigitalLogistics.Instance.Settings.TerminalBudgetMilliseconds.Value / 1000d;

        private sealed class GroupState
        {
            internal CachedItem Item;
            internal StorageTerminalGroup Group;
            internal StorageTerminalList.GroupView View;
            internal StorageTerminalRowController Header;
            internal bool Expanded;
            internal bool Queued;
            internal int RenderedItems;
            internal readonly Dictionary<int, StorageTerminalRowController> Rows = new();
            internal CachedItem FirstItem => Group?.Items[0] ?? Item;
        }

        private StorageTerminalUI view;
        private StorageTerminalList List => view.List;
        private readonly Dictionary<string, GroupState> groups = new();
        private readonly List<GroupState> orderedGroups = new();
        private readonly Queue<GroupState> pendingChildren = new Queue<GroupState>();
        private CancellationTokenSource searchCancellation;
        private TaskCompletionSource<bool> nextFrame;
        private double searchDeadline = double.PositiveInfinity;
        private readonly HashSet<string> expandedGroups = new();
        private StorageTerminalInteraction interaction;
        private readonly Dictionary<int, CachedItem> currentItems = new();
        private StoredItemExpression searchExpression;
        private IComparer<CachedItem> itemComparer;
        private IComparer<GroupState> groupComparer;
        private float? pendingScrollPosition;
        private bool subscribed;

        internal bool Refreshing => StorageIndexRefresh.Running;
        internal bool Interacting => Refreshing || interaction.Consuming;
        internal StorageTerminalConfiguration Configuration => view.Configuration;
        internal bool ContainsPointer(Vector3 position, TreeNodeRender ignored) => view.ContainsPointer(position, ignored);
        internal void Input(GameItem item, int amount) => interaction.InputExternal(item, amount);

        internal void Init(StorageTerminalUI view)
        {
            this.view = view;
            view.SearchChanged += OnSearchChanged;
            view.Configuration.Changed += OnConfigurationChanged;
            interaction = gameObject.AddComponent<StorageTerminalInteraction>();
            interaction.Init(this);
            view.RefreshButton.Clicked += Refresh;
            view.RefreshButton.Held += StorageIndexRefresh.Start;
            if (isActiveAndEnabled) Subscribe();
        }

        private void Subscribe()
        {
            if (subscribed) return;
            DigitalLogistics.Manager.onChange += OnStorageChanged;
            StorageIndexRefresh.StateChanged += OnRefreshStateChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            DigitalLogistics.Manager.onChange -= OnStorageChanged;
            StorageIndexRefresh.StateChanged -= OnRefreshStateChanged;
            subscribed = false;
        }

        internal void Open()
        {
            view.Configuration.RefreshDescriptions();
            view.RefreshButton.SetPending(false);
            view.SetRefreshing(Refreshing);
            if (Refreshing) return;
            Search(true);
        }

        private void Refresh()
        {
            if (!Refreshing) Search(true);
        }

        private void OnStorageChanged() => view.RefreshButton.SetPending(true);

        private void OnRefreshStateChanged()
        {
            if (Refreshing)
            {
                StopSearch();
                interaction.Suspend();
            }
            view.SetRefreshing(Refreshing);
            if (Refreshing || !isActiveAndEnabled) return;
            Search(true);
        }

        private void ClearResults()
        {
            foreach (var group in groups.Values) group.View.Destroy();
            groups.Clear();
            orderedGroups.Clear();
            currentItems.Clear();
            pendingChildren.Clear();
            pendingScrollPosition = null;
            searchExpression = null;
        }

        private async void Search(bool preservePosition = false)
        {
            StopSearch();
            if (!isActiveAndEnabled || Refreshing) return;

            float position = List.ScrollPosition;
            if (!preservePosition) expandedGroups.Clear();
            ClearResults();
            if (preservePosition) pendingScrollPosition = position;
            view.RefreshButton.SetPending(false);
            List.ResetScroll();
            List.RefreshPriceUnit();

            var expression = StorageTerminalSearchParser.Parse(view.SearchText);
            if (view.Configuration.Safety != StorageTerminalConfiguration.SafetyFilter.All)
            {
                var unsafeItems = StoredItemExpression.ContrabandLevel.EqualTo(0).Not()
                    .Or(StoredItemExpression.Heat.EqualTo(0).Not());
                expression = expression.And(view.Configuration.Safety == StorageTerminalConfiguration.SafetyFilter.Legal
                    ? unsafeItems.Not() : unsafeItems);
            }
            searchExpression = expression;
            var comparer = ComparerHelper.ComparedBy(Configuration.SortBy.Getter());
            itemComparer = Configuration.Reverse
                ? Comparer<CachedItem>.Create((left, right) => comparer.Compare(right, left)) : comparer;
            groupComparer = Comparer<GroupState>.Create((left, right) =>
                itemComparer.Compare(left.FirstItem, right.FirstItem));
            var cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            searchCancellation = cancellation;

            try
            {
                await using var enumerator = DigitalLogistics.Manager.SearchAsync(expression,
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
                        AddItem(enumerator.Current);
                    }

                    if (finished)
                    {
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

        private void UpdateSingle(GroupState state, CachedItem item)
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

        internal void RemoveItem(int uniqueId)
        {
            if (!currentItems.TryGetValue(uniqueId, out var item)) return;
            currentItems.Remove(uniqueId);
            var state = groups[item.DisplayName];
            if (state.Group == null)
            {
                state.View.Destroy();
                groups.Remove(item.DisplayName);
                orderedGroups.Remove(state);
                expandedGroups.Remove(item.DisplayName);
                return;
            }

            var items = state.Group.Items;
            int index = items.FindIndex(stored => stored.UniqueId == uniqueId);
            items.RemoveAt(index);
            if (items.Count == 1)
            {
                UpdateSingle(state, items[0]);
                PositionGroup(state);
                return;
            }

            if (state.Rows.TryGetValue(uniqueId, out var row))
            {
                row.View.Destroy();
                state.Rows.Remove(uniqueId);
            }
            if (index < state.RenderedItems) state.RenderedItems--;
            ReindexChildren(state, index);
            state.Group.SetItems(items.ToArray());
            UpdateSummary(state);
            QueueChildren(state);
            PositionGroup(state);
        }

        internal void InsertItem(CachedItem item)
        {
            if (searchExpression == null || !item.VerifyValidity() || !DigitalLogistics.Manager.Owns(item) || !searchExpression.Evaluate(item)
                || Configuration.SortBy.Getter()(item) == null)
            {
                RemoveItem(item.UniqueId);
                return;
            }

            if (!currentItems.TryGetValue(item.UniqueId, out var previous))
            {
                AddItem(item);
                return;
            }
            if (previous.DisplayName != item.DisplayName)
            {
                RemoveItem(item.UniqueId);
                AddItem(item);
                return;
            }

            currentItems[item.UniqueId] = item;
            var state = groups[item.DisplayName];
            if (state.Group == null) UpdateSingle(state, item);
            else
            {
                var items = state.Group.Items;
                int oldIndex = items.FindIndex(stored => stored.UniqueId == item.UniqueId);
                items.RemoveAt(oldIndex);
                int index = FindItemIndex(items, item);
                items.Insert(index, item);
                state.Group.SetItems(items.ToArray());
                if (state.Rows.TryGetValue(item.UniqueId, out var row)) UpdateRow(row, item);
                if (index != oldIndex)
                {
                    int start = Math.Min(index, oldIndex);
                    state.RenderedItems = Math.Min(state.RenderedItems, start);
                    ReindexChildren(state, start);
                }
                UpdateSummary(state);
                QueueChildren(state);
            }
            PositionGroup(state);
        }

        internal IEnumerable<CachedItem> GetGroupItems(string name)
        {
            var state = groups[name];
            return state.Group != null ? state.Group.Items : new[] { state.Item };
        }

        internal IEnumerable<CachedItem> GetCurrentItems(IEnumerable<int> uniqueIds)
        {
            if (Refreshing) yield break;
            foreach (int id in uniqueIds)
                if (currentItems.TryGetValue(id, out var item) && item.VerifyValidity()
                    && DigitalLogistics.Manager.Owns(item)) yield return item;
        }

        private void AddItem(CachedItem item)
        {
            if (!DigitalLogistics.Manager.Owns(item)) return;
            string name = item.DisplayName;
            if (currentItems.ContainsKey(item.UniqueId)) return;
            currentItems[item.UniqueId] = item;
            if (!groups.TryGetValue(name, out var state))
            {
                var row = new StorageTerminalRowController(this, item);
                var entry = List.CreateRow(row);
                row.View = entry.Header;
                UpdateRow(row, item);
                state = new GroupState { Item = item, View = entry, Header = row };
                groups.Add(name, state);
                PositionGroup(state, false);
                return;
            }
            if (state.Group == null) CreateGroup(state, name);
            var first = state.FirstItem;
            int index = FindItemIndex(state.Group.Items, item);
            state.Group.Insert(index, item);
            if (index < state.RenderedItems)
                state.RenderedItems = index;
            ReindexChildren(state, index);
            UpdateSummary(state);
            QueueChildren(state);
            if (itemComparer.Compare(first, state.FirstItem) != 0) PositionGroup(state);
        }

        private int FindItemIndex(List<CachedItem> items, CachedItem item)
        {
            if (items.Count == 0 || itemComparer.Compare(items[items.Count - 1], item) <= 0) return items.Count;
            int index = items.BinarySearch(item, itemComparer);
            return index < 0 ? ~index : index;
        }

        private void PositionGroup(GroupState state, bool existing = true)
        {
            if (existing) orderedGroups.Remove(state);
            int index = orderedGroups.Count;
            if (index > 0 && groupComparer.Compare(orderedGroups[index - 1], state) > 0)
            {
                index = orderedGroups.BinarySearch(state, groupComparer);
                if (index < 0) index = ~index;
            }
            orderedGroups.Insert(index, state);
            state.View.SetIndex(index);
        }

        private static void ReindexChildren(GroupState state, int start)
        {
            for (int i = start; i < state.Group.Items.Count; i++)
                if (state.Rows.TryGetValue(state.Group.Items[i].UniqueId, out var row)) row.View.SetIndex(i);
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
                if (!state.Expanded || state.Group == null || state.RenderedItems >= state.Group.Items.Count) continue;
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

        private void UpdateRow(StorageTerminalRowController row, CachedItem item)
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
            searchDeadline = Time.realtimeSinceStartupAsDouble + DigitalLogistics.Instance.Settings.SearchDelayMilliseconds.Value / 1000d;
        }

        private void OnConfigurationChanged() => Search();

        private void LateUpdate()
        {
            if (Refreshing) return;
            if (Time.realtimeSinceStartupAsDouble >= searchDeadline) Search();
            double deadline = Time.realtimeSinceStartupAsDouble + RefreshBudgetSeconds;
            RenderChildren(deadline);
            if (searchCancellation == null && pendingChildren.Count == 0 && pendingScrollPosition.HasValue)
            {
                List.RestoreScroll(pendingScrollPosition.Value);
                pendingScrollPosition = null;
            }
            ResumeFrame();
        }

        private void OnDisable()
        {
            activeTerminals--;
            Unsubscribe();
            StopSearch();
        }

        private void OnEnable()
        {
            activeTerminals++;
            if (view != null) Subscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            view.SearchChanged -= OnSearchChanged;
            view.Configuration.Changed -= OnConfigurationChanged;
            view.RefreshButton.Clicked -= Refresh;
            view.RefreshButton.Held -= StorageIndexRefresh.Start;
            view.Dispose();
        }
    }
}
