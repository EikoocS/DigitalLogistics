using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DigitalStorage.Storage;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DigitalStorage
{
    internal sealed class StorageTerminalList : MonoBehaviour
    {
        private const double RefreshBudgetSeconds = 0.002;
        private const double SearchDelaySeconds = 0.3;
        private const float RowHeight = 40;
        private const float RowSpacing = 4;
        private const float PriceColumnWidth = 144;
        private static readonly Color NormalRowColor = new(0.9f, 0.9f, 0.9f, 1);
        private static readonly Color ExpandedGroupColor = new(0.75f, 0.75f, 0.75f, 1);

        private sealed class RowView
        {
            internal StorageTerminalRow Element;
            internal TreeNodeRender Node;
            internal TreeNodeRender Label;
            internal TreeNodeRender Price;
        }

        private sealed class GroupView
        {
            internal StoredItem Item;
            internal StorageTerminalGroup Group;
            internal RectTransform Root;
            internal RectTransform Children;
            internal RowView Header;
            internal bool Expanded;
            internal bool Queued;
            internal int RenderedItems;
        }

        private ScrollRect scroll;
        private TMP_InputField searchInput;
        private TreeNodeRender rowParent;
        private readonly Dictionary<string, GroupView> groups = new();
        private readonly Queue<GroupView> pendingChildren = new Queue<GroupView>();
        private CancellationTokenSource searchCancellation;
        private TaskCompletionSource<bool> nextFrame;
        private double searchDeadline = double.PositiveInfinity;
        private string priceUnit;
        private StorageTerminalInteraction interaction;
        private readonly HashSet<string> expandedGroups = new();
        private bool inventoryChanged;
        private Vector2? restoreScrollPosition;

        internal void Init(TMP_InputField input, TreeNodeRender parent)
        {
            searchInput = input;
            rowParent = parent;
            scroll = GetComponentInChildren<ScrollRect>(true);
            scroll.horizontal = false;
            scroll.content.anchorMin = new Vector2(0, 1);
            scroll.content.anchorMax = Vector2.one;
            scroll.content.pivot = new Vector2(0.5f, 1);
            ConfigureLayout(scroll.content.GetComponent<VerticalLayoutGroup>());
            var fitter = scroll.content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            searchInput.onValueChanged.AddListener(OnSearchChanged);
            interaction = gameObject.AddComponent<StorageTerminalInteraction>();
            interaction.Init(this);
        }

        internal void Refresh() => Refresh(false);

        private async void Refresh(bool preserveState)
        {
            StopSearch();
            if (!isActiveAndEnabled) return;

            inventoryChanged = false;
            if (preserveState) restoreScrollPosition ??= scroll.content.anchoredPosition;
            else
            {
                restoreScrollPosition = null;
                expandedGroups.Clear();
            }
            scroll.StopMovement();
            foreach (var group in groups.Values)
            {
                group.Root.gameObject.SetActive(false);
                Destroy(group.Root.gameObject);
            }
            groups.Clear();
            pendingChildren.Clear();
            priceUnit = LocHelper.GetLocalizedMechanic("mech_credits");
            if (!preserveState) scroll.content.anchoredPosition = Vector2.zero;
            LayoutRebuilder.MarkLayoutForRebuild(scroll.content);

            var query = new StorageQuery(searchInput.text);
            var cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            searchCancellation = cancellation;

            try
            {
                await using var enumerator = DigitalStorage.Manager.SearchAsync(query, token).GetAsyncEnumerator();
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

                    if (finished) return;
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

        private void AddItem(StoredItem item)
        {
            string name = item.DisplayName;
            if (!groups.TryGetValue(name, out var view))
            {
                var row = CreateRow(scroll.content, item);
                groups.Add(name, new GroupView { Item = item, Root = row.Node.rectTransform, Header = row });
                return;
            }
            if (view.Group == null) CreateGroup(view, name);
            view.Group.Add(item);
            UpdateSummary(view);
            QueueChildren(view);
        }

        private void CreateGroup(GroupView view, string name)
        {
            int index = view.Root.GetSiblingIndex();
            var root = CreateContainer(scroll.content, "StorageTerminalGroup");
            root.SetSiblingIndex(index);
            var header = view.Header;
            header.Element.Item = null;
            header.Node.rectTransform.SetParent(root, false);
            var children = CreateContainer(root, "StorageTerminalChildren");
            children.GetComponent<VerticalLayoutGroup>().padding.left = 20;
            view.Expanded = expandedGroups.Contains(name);
            children.gameObject.SetActive(view.Expanded);

            view.Group = new StorageTerminalGroup(name);
            header.Element.Group = view.Group;
            view.Group.Add(view.Item);
            view.Item = null;
            view.Root = root;
            view.Children = children;
            UpdateHeader(view);
            header.Node.InitEventHandler();
            var events = header.Node.eventHandler;
            events.mouseClickFunc.Add(data =>
            {
                if (data.button == PointerEventData.InputButton.Left && !data.dragging)
                {
                    view.Expanded = !view.Expanded;
                    if (view.Expanded) expandedGroups.Add(name);
                    else expandedGroups.Remove(name);
                    view.Children.gameObject.SetActive(view.Expanded);
                    UpdateHeader(view);
                    QueueChildren(view);
                }
                return true;
            });
            events.mousePotentialDragFunc.Add(data => { scroll.OnInitializePotentialDrag(data); return true; });
            events.mouseBeginDragFunc.Add(data => { scroll.OnBeginDrag(data); return true; });
            events.mouseDragFunc.Add(data => { scroll.OnDrag(data); return true; });
            events.mouseEndDragFunc.Add(data => { scroll.OnEndDrag(data); return true; });
            events.mouseScrollFunc.Add(data => { scroll.OnScroll(data); return true; });
        }

        private static void UpdateHeader(GroupView view)
        {
            SetName(view.Header, view.Group.Name, view.Group.ContrabandLevel, view.Group.Heat, view.Group.Items.Count);
            view.Header.Node.image.color = view.Expanded
                ? ExpandedGroupColor
                : NormalRowColor;
        }

        private void UpdateSummary(GroupView view)
        {
            view.Header.Price.text.text = FormatPrice(view.Group.TotalPrice);
            SetName(view.Header, view.Group.Name, view.Group.ContrabandLevel, view.Group.Heat, view.Group.Items.Count);
        }

        private static void SetName(RowView row, string name, int contrabandLevel, int? heat, int? count = null)
        {
            string text = $"<noparse>{name}</noparse>";
            if (count.HasValue) text += $" <b><i>*{count.Value}</i></b>";
            string contraband = contrabandLevel switch
            {
                1 => "[I]",
                2 => "[II]",
                3 => "[III]",
                4 => "[IV]",
                _ => ""
            };
            if (contraband.Length > 0) text += FormatStatus(contraband, (contrabandLevel - 1) / 3f);
            if (heat.HasValue) text += FormatStatus($"[{heat.Value}%]", heat.Value / 100f);
            row.Label.text.text = text;
        }

        private static string FormatStatus(string text, float severity)
        {
            var color = Color.Lerp(Color.white, MachineryStyle.PROGRESS_FILL_DANGER, severity);
            return $" <color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";
        }

        private string FormatPrice(long price) => $"{price:N0} {priceUnit}";

        private void QueueChildren(GroupView view)
        {
            if (!view.Expanded || view.Queued || view.RenderedItems == view.Group.Items.Count) return;
            view.Queued = true;
            pendingChildren.Enqueue(view);
        }

        private void RenderChildren(double deadline)
        {
            while (pendingChildren.Count > 0 && Time.realtimeSinceStartupAsDouble < deadline)
            {
                var view = pendingChildren.Dequeue();
                view.Queued = false;
                if (!view.Expanded) continue;
                CreateRow(view.Children, view.Group.Items[view.RenderedItems++]);
                QueueChildren(view);
            }
        }

        private static RectTransform CreateContainer(RectTransform parent, string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            var rect = (RectTransform)root.transform;
            rect.SetParent(parent, false);
            ConfigureLayout(root.GetComponent<VerticalLayoutGroup>());
            return rect;
        }

        private static void ConfigureLayout(VerticalLayoutGroup layout)
        {
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = RowSpacing;
        }

        private RowView CreateRow(RectTransform parent, StoredItem item)
        {
            var element = new StorageTerminalRow(this, item);
            var row = element.handler.SetParent(rowParent);
            row.gameObject.name = "StorageTerminalItem";
            row.rectTransform.SetParent(parent, false);
            row.image.pixelsPerUnitMultiplier = 1 / CustomUIMachineryStyle.SCALE;
            row.InitEventHandler();
            row.eventHandler.mouseScrollFunc.Add(data => { scroll.OnScroll(data); return true; });
            MachineryStyle.SetSliced(row, MachineryStyle.BevelFrame, NormalRowColor);

            var size = row.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = RowHeight;
            size.flexibleHeight = 0;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 4, 4);
            layout.spacing = 8;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var label = CreateLabel(row, HorizontalAlignmentOptions.Left);
            label.text.richText = true;
            var price = CreateLabel(row, HorizontalAlignmentOptions.Right, PriceColumnWidth);
            price.text.text = FormatPrice(item.Price);
            price.text.color = MachineryStyle.PROGRESS_FILL_WARNING;
            var view = new RowView
            {
                Element = element,
                Node = row,
                Label = label,
                Price = price
            };
            SetName(view, item.DisplayName, item.ContrabandLevel, item.Heat);
            return view;
        }

        private static TreeNodeRender CreateLabel(TreeNodeRender parent, HorizontalAlignmentOptions alignment,
            float width = 0)
        {
            var label = TreeNodeRender.InstantiateText().SetParent(parent);
            MachineryStyle.InitText(label, 16, MachineryStyle.BODY_TEXT, alignment, false);
            label.text.richText = false;
            label.text.enableWordWrapping = false;
            label.text.fontSizeMin = 3;
            label.text.fontSizeMax = 16;
            label.text.enableAutoSizing = true;
            label.text.overflowMode = TextOverflowModes.Ellipsis;
            label.text.raycastTarget = false;
            var size = label.gameObject.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = width;
            size.flexibleWidth = width > 0 ? 0 : 1;
            return label;
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

        private void LateUpdate()
        {
            if (Time.realtimeSinceStartupAsDouble >= searchDeadline) Refresh();
            else if (inventoryChanged && !interaction.PointerDown) Refresh(true);
            double deadline = Time.realtimeSinceStartupAsDouble + RefreshBudgetSeconds;
            RenderChildren(deadline);
            ResumeFrame();
            if (restoreScrollPosition.HasValue && searchCancellation == null && pendingChildren.Count == 0)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                scroll.content.anchoredPosition = restoreScrollPosition.Value;
                restoreScrollPosition = null;
            }
        }

        private void OnInventoryChanged()
        {
            inventoryChanged = true;
            searchCancellation?.Cancel();
            ResumeFrame();
        }

        private void OnEnable()
        {
            DigitalStorage.Manager.Changed += OnInventoryChanged;
            if (interaction != null) interaction.enabled = true;
        }

        private void OnDisable()
        {
            DigitalStorage.Manager.Changed -= OnInventoryChanged;
            StopSearch();
            if (interaction != null) interaction.Disable();
        }

        private void OnDestroy()
        {
            searchInput.onValueChanged.RemoveListener(OnSearchChanged);
        }
    }
}
