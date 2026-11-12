using DigitalStorage.Item;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DigitalStorage
{
    internal sealed class StorageTerminalUI
    {
        private readonly PixelWindow window;

        internal static PixelWindow Create() => new StorageTerminalUI().window;

        private StorageTerminalUI()
        {
            var (inputHost, inputRoot) = CreateControl(CustomUIManager.ElementKind.Input, 232, 18);
            var input = CustomUIElement.Bind(inputRoot, "search");
            input.SetString(string.Empty);
            var searchInput = input.input;
            searchInput.lineType = TMP_InputField.LineType.SingleLine;
            ((TMP_Text)searchInput.placeholder).text =
                StorageTerminal.Localize("ui.storage_terminal.search_placeholder");

            var (listHost, listRoot) = CreateControl(CustomUIManager.ElementKind.ScrollView, 232, 170);
            var list = listRoot.AddComponent<StorageTerminalList>();
            list.Init(searchInput, listHost.handler);

            var body = MachineryStack.Vertical(inputHost, listHost)
                .SetAlign(MachineryStack.Align.Start)
                .SetMainAlign(MachineryStack.Align.Start);
            window = MachineHelper.CreateMachineryWindow(true, MachineHelper.Panel(null, body));
            window.openCallback = list.Refresh;
        }

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
