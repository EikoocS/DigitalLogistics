using System;
using DigitalStorage.Controller;
using DigitalStorage.Item;
using TMPro;
using UnityEngine;

namespace DigitalStorage.UI
{
    internal sealed class StorageTerminalUI
    {
        private readonly PixelWindow window;
        private readonly TMP_InputField searchInput;

        internal StorageTerminalList List { get; }
        internal StorageTerminalConfiguration Configuration { get; }
        internal string SearchText => searchInput.text;
        internal event Action<string> SearchChanged;

        internal static PixelWindow Create() => new StorageTerminalUI().window;

        private StorageTerminalUI()
        {
            var (inputHost, inputRoot) = CreateControl(CustomUIManager.ElementKind.Input, 232, 18);
            var input = CustomUIElement.Bind(inputRoot, "search");
            input.SetString(string.Empty);
            searchInput = input.input;
            searchInput.lineType = TMP_InputField.LineType.SingleLine;
            ((TMP_Text)searchInput.placeholder).text =
                StorageTerminal.Localize("ui.storage_terminal.search_placeholder");

            var (listHost, listRoot) = CreateControl(CustomUIManager.ElementKind.ScrollView, 232, 170);
            List = new StorageTerminalList(listRoot, listHost.handler);

            var content = MachineryStack.Vertical(inputHost, listHost)
                .SetAlign(MachineryStack.Align.Start)
                .SetMainAlign(MachineryStack.Align.Start);
            Configuration = new StorageTerminalConfiguration();
            var body = MachineryStack.Horizontal(Configuration.Layout, content)
                .SetAlign(MachineryStack.Align.Start)
                .SetMainAlign(MachineryStack.Align.Start);
            window = MachineHelper.CreateMachineryWindow(true, MachineHelper.Panel(null, body));
            searchInput.onValueChanged.AddListener(OnSearchChanged);
            var controller = listRoot.AddComponent<StorageTerminalController>();
            controller.Init(this);
            window.openCallback = controller.Refresh;
        }

        private void OnSearchChanged(string text) => SearchChanged?.Invoke(text);

        internal void Dispose() => searchInput.onValueChanged.RemoveListener(OnSearchChanged);

        private static (EmptyPixelElement Host, GameObject Root) CreateControl(
            CustomUIManager.ElementKind kind, int width, int height)
        {
            var host = new EmptyPixelElement(width, height);
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
