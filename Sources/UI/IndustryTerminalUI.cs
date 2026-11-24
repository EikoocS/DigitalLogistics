using System.Linq;
using DigitalLogistics.Item;
using UnityEngine;
using UnityEngine.UI;
using static DigitalLogistics.UI.Common.MachineryControlFactory;

namespace DigitalLogistics.UI
{
    internal sealed class IndustryTerminalUI : MonoBehaviour
    {
        private ScrollRect scroll;

        internal static PixelWindow Create()
        {
            var (host, root) = CreateControl(CustomUIManager.ElementKind.ScrollView, 208, 118);
            var window = MachineHelper.CreateMachineryWindow(true, MachineHelper.Panel(null, host));
            var ui = root.AddComponent<IndustryTerminalUI>();
            ui.scroll = root.GetComponentInChildren<ScrollRect>(true);
            ui.scroll.horizontal = false;
            var content = ui.scroll.content;
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);

            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 4;
            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return window;
        }

        private void OnEnable()
        {
            StorageDrive.IndustryDisksChanged += Refresh;
            Refresh();
        }

        private void OnDisable() => StorageDrive.IndustryDisksChanged -= Refresh;

        private void Refresh()
        {
            if (scroll == null) return;
            var content = scroll.content;
            foreach (Transform row in content)
            {
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }

            var disks = StorageDrive.MountedIndustryDisks().OrderBy(disk => disk.uniqueId).ToArray();
            if (disks.Length == 0)
                AddRow(DigitalLogistics.Localize("ui.industry_terminal.no_disks"), null);
            foreach (var disk in disks)
                AddRow($"{disk.GetDisplayName(preferCustomName: true)}  #{disk.uniqueId}", disk);
        }

        private void AddRow(string title, IndustryDisk disk)
        {
            var root = CustomUIManager.Instance.SpawnElement(CustomUIManager.ElementKind.Button,
                scroll.content, null, "IndustryDisk");
            CustomUIMachineryStyle.Reskin(root);
            var row = CustomUIElement.Bind(root, "disk");
            row.SetText(title);
            row.text.richText = false;
            row.text.enableWordWrapping = false;
            row.text.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            var size = root.GetComponent<LayoutElement>() ?? root.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = 40;
            row.button.interactable = disk != null;
            if (disk != null) row.button.onClick.AddListener(disk.OpenArea);
        }
    }
}
