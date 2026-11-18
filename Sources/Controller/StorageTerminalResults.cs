using DigitalLogistics.Storage.Items;
using DigitalLogistics.Storage.Indexing;
using System;
using System.Collections.Generic;
using DigitalLogistics.Collections;
using DigitalLogistics.Filtering;
using DigitalLogistics.Storage;
using DigitalLogistics.UI;
using UnityEngine;

namespace DigitalLogistics.Controller
{
    internal sealed class StorageTerminalResults
    {
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

        private readonly StorageTerminalController owner;
        private readonly StorageTerminalList List;
        private readonly StorageSort.SortBy sortBy;
        private readonly StoredItemExpression searchExpression;
        private readonly IComparer<CachedItem> itemComparer;
        private readonly IComparer<GroupState> groupComparer;
        private readonly Dictionary<string, GroupState> groups = new();
        private readonly List<GroupState> orderedGroups = new();
        private readonly Queue<GroupState> pendingChildren = new();
        private readonly Dictionary<int, CachedItem> currentItems = new();
        private readonly HashSet<string> expandedGroups;

        internal RectTransform Content { get; }
        internal IEnumerable<string> ExpandedGroups => expandedGroups;
        internal bool HasPendingChildren => pendingChildren.Count > 0;

        internal StorageTerminalResults(StorageTerminalController owner, StorageTerminalList list,
            StoredItemExpression expression, StorageSort.SortBy sortBy, bool reverse, IEnumerable<string> expanded)
        {
            this.owner = owner;
            List = list;
            this.sortBy = sortBy;
            searchExpression = expression;
            expandedGroups = expanded == null ? new() : new(expanded);
            Content = list.CreateStagingContent();
            var comparer = sortBy.Comparer();
            itemComparer = reverse
                ? Comparer<CachedItem>.Create((left, right) => comparer.Compare(right, left)) : comparer;
            groupComparer = Comparer<GroupState>.Create((left, right) =>
                itemComparer.Compare(left.FirstItem, right.FirstItem));
        }

        internal void SetExpanded(string name, bool expanded)
        {
            if (expanded) expandedGroups.Add(name);
            else expandedGroups.Remove(name);
            if (!groups.TryGetValue(name, out var state) || state.Group == null || state.Expanded == expanded) return;
            state.Expanded = expanded;
            state.View.SetExpanded(expanded);
            UpdateSummary(state);
            QueueChildren(state);
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
                || sortBy.Getter()(item) == null)
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
            foreach (int id in uniqueIds)
                if (currentItems.TryGetValue(id, out var item) && item.VerifyValidity()
                    && DigitalLogistics.Manager.Owns(item)) yield return item;
        }

        internal void AddItem(CachedItem item)
        {
            if (!DigitalLogistics.Manager.Owns(item)) return;
            string name = item.DisplayName;
            if (currentItems.ContainsKey(item.UniqueId)) return;
            currentItems[item.UniqueId] = item;
            if (!groups.TryGetValue(name, out var state))
            {
                var row = new StorageTerminalRowController(owner, item);
                var entry = List.CreateRow(Content, row);
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
            SetExpanded(name, !state.Expanded);
            owner.OnGroupExpanded(this, name, state.Expanded);
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

        internal void RenderChildren(double deadline)
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
                    row = new StorageTerminalRowController(owner, item);
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
    }
}
