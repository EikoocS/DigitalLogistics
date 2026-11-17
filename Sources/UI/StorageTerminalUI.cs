using System;
using DigitalStorage.Controller;
using DigitalStorage.Item;
using DigitalStorage.Storage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DigitalStorage.UI
{
    internal sealed class StorageTerminalUI
    {
        private readonly PixelWindow window;
        private readonly TMP_InputField searchInput;
        private readonly RectTransform dropArea;
        private readonly TreeNodeRender listHandler;
        private readonly StorageTerminalController controller;
        private readonly ScrollRect scroll;
        private readonly TreeNodeRender refreshOverlay;
        private readonly Button refreshButton;

        internal StorageTerminalList List { get; }
        internal StorageTerminalConfiguration Configuration { get; }
        internal StorageTerminalRefreshButton RefreshButton { get; }
        internal string SearchText => searchInput.text;
        internal event Action<string> SearchChanged;

        internal static PixelWindow Create(GameItem item) => new StorageTerminalUI(item).window;

        private StorageTerminalUI(GameItem item)
        {
            var (inputHost, inputRoot) = CreateControl(CustomUIManager.ElementKind.Input, 204, 18);
            var input = CustomUIElement.Bind(inputRoot, "search");
            input.SetString(string.Empty);
            searchInput = input.input;
            searchInput.lineType = TMP_InputField.LineType.SingleLine;
            ((TMP_Text)searchInput.placeholder).text =
                StorageTerminal.Localize("ui.storage_terminal.search_placeholder");

            var (refreshHost, refreshRoot) = CreateControl(CustomUIManager.ElementKind.Button, 24, 18);
            var refresh = CustomUIElement.Bind(refreshRoot, "refresh");
            refresh.SetText(string.Empty);
            refreshButton = refresh.button;
            RefreshButton = refreshButton.gameObject.AddComponent<StorageTerminalRefreshButton>();
            RefreshButton.Init(refreshButton);
            var searchRow = MachineryStack.Horizontal(inputHost, refreshHost)
                .SetAlign(MachineryStack.Align.Start).SetMainAlign(MachineryStack.Align.Start);

            var (listHost, listRoot) = CreateControl(CustomUIManager.ElementKind.ScrollView, 232, 142);
            List = new StorageTerminalList(listRoot, listHost.handler);
            scroll = listRoot.GetComponentInChildren<ScrollRect>(true);
            dropArea = scroll.viewport;
            listHandler = listHost.handler;
            listHandler.InitEventHandler();
            listHandler.image.enabled = true;
            listHandler.image.color = Color.clear;
            listHandler.image.alphaHitTestMinimumThreshold = 0;

            var content = MachineryStack.Vertical(searchRow, listHost)
                .SetAlign(MachineryStack.Align.Start)
                .SetMainAlign(MachineryStack.Align.Start);
            Configuration = new StorageTerminalConfiguration(item);
            var body = MachineryStack.Horizontal(Configuration.Layout, content)
                .SetAlign(MachineryStack.Align.Start)
                .SetMainAlign(MachineryStack.Align.Start);
            window = MachineHelper.CreateMachineryWindow(true, MachineHelper.Panel(null, body));
            refreshOverlay = TreeNodeRender.Instantiate().SetParent(body.inner);
            refreshOverlay.InitEventHandler();
            refreshOverlay.image.enabled = true;
            refreshOverlay.image.color = new Color(0.08f, 0.1f, 0.12f, 0.92f);
            Stretch(refreshOverlay.rectTransform);
            var busyText = TreeNodeRender.InstantiateText().SetParent(refreshOverlay);
            MachineryStyle.InitText(busyText, 8, MachineryStyle.BODY_TEXT, HorizontalAlignmentOptions.Center);
            busyText.text.text = StorageTerminal.Localize("ui.storage_terminal.refreshing");
            busyText.text.raycastTarget = false;
            Stretch(busyText.rectTransform);
            refreshOverlay.gameObject.SetActive(false);
            searchInput.onValueChanged.AddListener(OnSearchChanged);
            controller = listRoot.AddComponent<StorageTerminalController>();
            controller.Init(this);
            window.openCallback = controller.Open;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        internal void SetRefreshing(bool value)
        {
            searchInput.DeactivateInputField();
            searchInput.interactable = !value;
            refreshButton.interactable = !value;
            Configuration.SetEnabled(!value);
            scroll.enabled = !value;
            refreshOverlay.gameObject.SetActive(value);
            refreshOverlay.rectTransform.SetAsLastSibling();
        }

        internal static void NotifyRefreshResult()
        {
            if (!DigitalStorage.Instance.Settings.NotifyRefreshResult.Value) return;
            string message = string.Format(Localize("refresh_result"),
                StorageIndexRefresh.ProcessedItems, StorageIndexRefresh.ElapsedMilliseconds);
            if (!StorageIndexRefresh.Succeeded)
                message = $"<color=#{ColorUtility.ToHtmlStringRGB(MachineryStyle.PROGRESS_FILL_DANGER)}>{message}</color>";
            ReputationUIManager.Instance.Notify(message);
        }

        private static string Localize(string key) => StorageTerminal.Localize("ui.storage_terminal." + key);

        private void OnSearchChanged(string text) => SearchChanged?.Invoke(text);

        internal void Dispose() => searchInput.onValueChanged.RemoveListener(OnSearchChanged);

        internal static StorageTerminalController FindController(Vector3 position, TreeNodeRender ignored)
        {
            var row = RenderHandler.RaycastFindParentElement<StorageTerminalRow>(position, ignored);
            if (row != null) return row.Controller.Owner;
            var host = RenderHandler.RaycastFindParentElement<EmptyPixelElement>(position, ignored);
            return host?.handler.GetComponentInChildren<StorageTerminalController>();
        }

        internal bool ContainsPointer(Vector3 position, TreeNodeRender ignored)
        {
            var hit = RenderHandler.RaycastFindParentElement<StorageTerminalRow>(position, ignored);
            if (hit != null) return hit.Controller.Owner == controller;
            return RectTransformUtility.RectangleContainsScreenPoint(dropArea, position)
                && RenderHandler.RaycastFindParentElement<EmptyPixelElement>(position, ignored)?.handler == listHandler;
        }

        internal static (EmptyPixelElement Host, GameObject Root) CreateControl(
            CustomUIManager.ElementKind kind, int width, int height, bool canResize = false)
        {
            var host = new EmptyPixelElement(width, height, canResize);
            host.handler.image.enabled = false;
            host.rectTransform.sizeDelta = new Vector2(width, height);
            var root = CustomUIManager.Instance.SpawnElement(kind, host.handler.transform, null, kind.ToString());
            CustomUIMachineryStyle.Reskin(root);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width * 2, height * 2);
            rect.localScale = new Vector3(0.5f, 0.5f, 1);
            return (host, root);
        }
    }
}
