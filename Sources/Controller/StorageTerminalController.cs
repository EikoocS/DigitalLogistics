using DigitalLogistics.Storage.Items;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DigitalLogistics.Filtering;
using DigitalLogistics.Storage;
using DigitalLogistics.Tracing;
using DigitalLogistics.UI;
using UnityEngine;

namespace DigitalLogistics.Controller
{
    internal sealed class StorageTerminalController : MonoBehaviour
    {
        private static int activeTerminals;
        internal static bool HasActiveTerminal => activeTerminals > 0;
        private static double RefreshBudgetSeconds => DigitalLogistics.Instance.Settings.TerminalBudgetMilliseconds.Value / 1000d;
        private static TerminalRefreshMode RefreshMode => DigitalLogistics.Instance.Settings.RefreshMode;

        private StorageTerminalUI view;
        private StorageTerminalList List => view.List;
        private StorageTerminalInteraction interaction;
        private StorageTerminalResults visible;
        private StorageTerminalResults staging;
        private CancellationTokenSource searchCancellation;
        private TaskCompletionSource<bool> nextFrame;
        private StoreClientInstance customer;
        private double searchDeadline = double.PositiveInfinity;
        private readonly HashSet<int> stagedEdits = new();
        private bool preserveStagedPosition;
        private bool storageChanged;
        private bool subscribed;

        internal bool Refreshing => StorageIndexRefresh.Running;
        internal bool Interacting => Refreshing || interaction.Consuming;
        internal StorageTerminalConfiguration Configuration => view.Configuration;
        internal bool ContainsPointer(Vector3 position, TreeNodeRender ignored) => view.ContainsPointer(position, ignored);
        internal void Input(GameItem item, int amount) => interaction.InputExternal(item, amount);

        private bool InputInProgress => interaction.HasActiveInput
            || ItemMouseDragHandler.current?.IsDraggingItem == true
            || ItemMultiSelectHandler.current?.IsGroupDragging == true;

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
            DigitalLogistics.Instance.Settings.TerminalRefresh.OnChanged += OnRefreshModeChanged;
            StorageIndexRefresh.StateChanged += OnRefreshStateChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            DigitalLogistics.Manager.onChange -= OnStorageChanged;
            DigitalLogistics.Instance.Settings.TerminalRefresh.OnChanged -= OnRefreshModeChanged;
            StorageIndexRefresh.StateChanged -= OnRefreshStateChanged;
            subscribed = false;
        }

        internal void Open()
        {
            UpdateCustomer();
            Configuration.RefreshDescriptions();
            view.SetRefreshing(Refreshing);
            if (!Refreshing) Search(true);
        }

        private void Refresh()
        {
            if (!Refreshing && RefreshMode != TerminalRefreshMode.Automatic) Search(true);
        }

        private void OnStorageChanged()
        {
            storageChanged = true;
            UpdatePendingIndicator();
        }

        private void OnRefreshModeChanged(string _) => UpdatePendingIndicator();

        private void UpdatePendingIndicator() =>
            view.RefreshButton.SetPending(storageChanged && RefreshMode == TerminalRefreshMode.Notify);

        private void OnRefreshStateChanged()
        {
            if (Refreshing)
            {
                StopSearch();
                interaction.Suspend();
            }
            view.SetRefreshing(Refreshing);
            if (Refreshing || !isActiveAndEnabled) return;
            UpdateCustomer();
            Search(true);
        }

        private bool UpdateCustomer()
        {
            var store = PlayerStore.Instance;
            var current = store != null && store.isClientArrived ? store.currentClientInstance : null;
            if (current?.GetClientBlueprint().clientIntent == StoreClient.ClientIntent.SELL) current = null;
            if (ReferenceEquals(current, customer)) return false;

            bool wasActive = Configuration.SmartFilterActive;
            customer = current;
            Configuration.SmartFilterActive = customer != null && DigitalLogistics.Instance.Settings.AutoSmartFilter.Value;
            Configuration.SetCustomerAvailable(customer != null);
            return wasActive || Configuration.SmartFilterActive;
        }

        private CachedItemExpression BuildSearchExpression()
        {
            var expression = Configuration.SmartFilterActive
                ? CustomerItemFilter.Build(customer.GetClientBlueprint())
                : StorageTerminalSearchParser.Parse(view.SearchText);
            if (Configuration.Safety == StorageTerminalConfiguration.SafetyFilter.All) return expression;

            var unsafeItems = CachedItemExpression.ContrabandLevel.EqualTo(0).Not()
                .Or(CachedItemExpression.Heat.EqualTo(0).Not());
            var safety = Configuration.Safety == StorageTerminalConfiguration.SafetyFilter.Legal
                ? unsafeItems.Not() : unsafeItems;
            return safety.And(expression);
        }

        private async void Search(bool preservePosition = false)
        {
            StopSearch();
            if (!isActiveAndEnabled || Refreshing) return;

            var trace = SearchMeasurement.Begin();
            storageChanged = false;
            UpdatePendingIndicator();
            List.RefreshPriceUnit();
            var expression = BuildSearchExpression();
            var sortBy = Configuration.SortBy;
            bool reverse = Configuration.Reverse;
            trace?.BeginListWork();
            var results = new StorageTerminalResults(this, List, expression, sortBy, reverse,
                preservePosition ? visible?.ExpandedGroups : null);
            trace?.EndListWork();
            staging = results;
            preserveStagedPosition = preservePosition;
            var cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            searchCancellation = cancellation;
            var outcome = SearchOutcome.Failed;

            try
            {
                await using (var enumerator = DigitalLogistics.Manager.SearchAsync(trace, expression, sortBy, reverse,
                    cancellationToken: token).GetAsyncEnumerator())
                {
                    double deadline = Time.realtimeSinceStartupAsDouble + RefreshBudgetSeconds;
                    while (await enumerator.MoveNextAsync())
                    {
                        token.ThrowIfCancellationRequested();
                        if (!stagedEdits.Contains(enumerator.Current.UniqueId))
                        {
                            trace?.BeginListWork();
                            results.AddItem(enumerator.Current);
                            trace?.EndListWork();
                        }
                        if (Time.realtimeSinceStartupAsDouble < deadline) continue;
                        await WaitForNextFrame();
                        token.ThrowIfCancellationRequested();
                        deadline = Time.realtimeSinceStartupAsDouble + RefreshBudgetSeconds;
                    }
                }

                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    trace?.BeginListWork();
                    results.RenderChildren(Time.realtimeSinceStartupAsDouble + RefreshBudgetSeconds);
                    trace?.EndListWork();
                    if (!results.HasPendingChildren && !InputInProgress) break;
                    await WaitForNextFrame();
                }

                trace?.BeginListWork();
                List.Publish(results.Content, preservePosition ? List.ScrollPosition : 0);
                trace?.EndListWork();
                visible = results;
                outcome = SearchOutcome.Completed;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                outcome = SearchOutcome.Cancelled;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                trace?.Record(outcome, results.ResultCount);
                if (ReferenceEquals(staging, results)) staging = null;
                if (!ReferenceEquals(visible, results)) StorageTerminalList.Discard(results.Content);
                if (ReferenceEquals(searchCancellation, cancellation)) searchCancellation = null;
                cancellation.Dispose();
            }
        }

        internal void RemoveItem(int uniqueId)
        {
            visible?.RemoveItem(uniqueId);
            if (staging == null) return;
            stagedEdits.Add(uniqueId);
            staging.RemoveItem(uniqueId);
        }

        internal void InsertItem(CachedItem item)
        {
            visible?.InsertItem(item);
            if (staging == null) return;
            stagedEdits.Add(item.UniqueId);
            staging.InsertItem(item);
        }

        internal IEnumerable<CachedItem> GetGroupItems(string name) =>
            visible?.GetGroupItems(name) ?? Array.Empty<CachedItem>();

        internal IEnumerable<CachedItem> GetCurrentItems(IEnumerable<int> uniqueIds) =>
            Refreshing || visible == null ? Array.Empty<CachedItem>() : visible.GetCurrentItems(uniqueIds);

        internal void OnGroupExpanded(StorageTerminalResults source, string name, bool expanded)
        {
            if (ReferenceEquals(visible, source) && preserveStagedPosition)
                staging?.SetExpanded(name, expanded);
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
            searchCancellation = null;
            staging = null;
            stagedEdits.Clear();
            ResumeFrame();
        }

        private void OnSearchChanged(string text)
        {
            UpdateCustomer();
            Configuration.SmartFilterActive = false;
            StopSearch();
            searchDeadline = Time.realtimeSinceStartupAsDouble + DigitalLogistics.Instance.Settings.SearchDelayMilliseconds.Value / 1000d;
        }

        private void OnConfigurationChanged() => Search();

        private void LateUpdate()
        {
            bool customerFilterChanged = UpdateCustomer();
            if (Refreshing) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (customerFilterChanged || now >= searchDeadline) Search();
            else if (storageChanged && RefreshMode == TerminalRefreshMode.Automatic
                && searchCancellation == null && double.IsPositiveInfinity(searchDeadline) && !InputInProgress)
                Search(true);
            visible?.RenderChildren(Time.realtimeSinceStartupAsDouble + RefreshBudgetSeconds);
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
