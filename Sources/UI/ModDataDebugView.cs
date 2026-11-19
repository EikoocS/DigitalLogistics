using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DigitalLogistics.UI
{
    internal sealed class ModDataDebugView
    {
        private const string WindowId = "digital_logistics__mod_data_debug";
        private const string EditorWindowId = "digital_logistics__mod_data_entry_debug";
        private const int PreviewLength = 120;
        private const float OpenColumnWidth = 80;
        private const string SaveNotice = "Changes are written on the next game save.";
        private const string EditorNotice = SaveNotice + " Reload the save to apply them to equipment.";

        private readonly CustomUIBuilder rows;
        private readonly Button add;

        private static string ModId => ModDataDebugEdits.ModId;
        private static bool HasSave => PlayerStore.HasInstance && PlayerStore.Instance.saveSlotId >= 0;

        internal static void Open(string parentId)
        {
            var builder = DebugMenu.CreateWindow(WindowId, "Mod save data", parentId);
            var view = new ModDataDebugView(builder);
            CustomUIDebugPanel.SetBackgroundAlpha(builder.Show(), 0.8f);
            view.Refresh();
        }

        internal static void Close()
        {
            CustomUIManager.Instance?.CloseWindow(EditorWindowId);
            CustomUIManager.Instance?.CloseWindow(WindowId);
        }

        private ModDataDebugView(CustomUIBuilder builder)
        {
            builder.BeginRow();
            builder.CurrentContainer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            add = AddButton(builder, "Add key", () => OpenEntry()).button;
            AddButton(builder, "Refresh", Refresh);
            builder.End();
            builder.AddLabel(SaveNotice).Last.SetFlexible(1, 0);

            builder.BeginScrollFlexible();
            rows = new CustomUIBuilder(CustomUIManager.Instance, builder.Handle, builder.CurrentContainer);
            builder.End();
        }

        private void Refresh()
        {
            var container = rows.CurrentContainer;
            CustomUIBuilder.ClearContainer(container);
            add.interactable = HasSave;
            if (!add.interactable)
            {
                rows.AddLabel("No save loaded.");
            }
            else
            {
                var keys = ModHelper.GetModDataKeys(ModId);
                AddRow("Key", "Value preview");
                foreach (var key in keys.OrderBy(key => key, StringComparer.Ordinal))
                    AddRow(Preview(key), Preview(ModHelper.GetModData(ModId, key)), () => OpenEntry(key));
                if (keys.Count == 0) rows.AddLabel("No entries.");
            }
            CustomUIBuilder.RebuildLayout(container);
        }

        private void AddRow(string key, string value, Action open = null)
        {
            rows.BeginRow(spacing: 8, alignment: TextAnchor.MiddleLeft);
            rows.CurrentContainer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            AddCell(key, 2);
            AddCell(value, 3);
            if (open == null) rows.AddSpace(OpenColumnWidth);
            else AddButton(rows, "Open", open).SetFixedWidth(OpenColumnWidth);
            rows.End();

            void AddCell(string text, float weight)
            {
                if (open == null) rows.AddHeader(text);
                else rows.AddLabel(text);
                var label = rows.Last.SetFlexible(weight, 0);
                var layout = label.root.GetComponent<LayoutElement>();
                layout.minWidth = layout.preferredWidth = 0;
                label.text.richText = false;
                label.text.enableAutoSizing = false;
                label.text.enableWordWrapping = false;
                label.text.overflowMode = TextOverflowModes.Ellipsis;
                label.text.alignment = TextAlignmentOptions.MidlineLeft;
            }
        }

        private void OpenEntry(string key = null)
        {
            bool adding = key == null;
            string original = adding ? "" : ModHelper.GetModData(ModId, key, "");
            var builder = DebugMenu.CreateWindow(EditorWindowId, adding ? "Add mod data key" : "Edit mod data", WindowId);
            builder.AddLabel("Key");
            var keyInput = AddInput(builder, "Key", TMP_InputField.LineType.SingleLine);
            keyInput.readOnly = !adding;
            keyInput.SetTextWithoutNotify(key ?? "");
            builder.AddLabel("Value");
            var valueInput = AddInput(builder, "Value", TMP_InputField.LineType.MultiLineNewline);
            builder.Last.SetFlexible(1, 1);
            valueInput.textComponent.alignment = TextAlignmentOptions.TopLeft;
            valueInput.SetTextWithoutNotify(original);

            builder.AddLabel(EditorNotice);
            var notice = builder.Last.SetFlexible(1, 0);
            builder.BeginRow();
            var apply = AddButton(builder, adding ? "Add key" : "Apply", () =>
            {
                ModDataDebugEdits.Set(adding ? keyInput.text : key, valueInput.text);
                Finish();
            }).button;
            if (!adding)
            {
                AddButton(builder, "Delete", () => OverlayHandler.current.ShowConfirmation("Delete mod data key",
                    new RichTextBuilder().Add(SaveNotice).Build(), () =>
                    {
                        if (!builder.Handle.IsAlive) return;
                        ModDataDebugEdits.Remove(key);
                        Finish();
                    }, yes: "Delete", yesColor: ButtonElement.Color.Red));
            }
            AddButton(builder, "Cancel", builder.Handle.Close);
            builder.End();

            keyInput.onValueChanged.AddListener(_ => UpdateApply());
            valueInput.onValueChanged.AddListener(_ => UpdateApply());
            UpdateApply();
            CustomUIDebugPanel.SetBackgroundAlpha(builder.Show(), 0.8f);

            void UpdateApply()
            {
                bool exists = adding && ModHelper.HasModData(ModId, keyInput.text);
                apply.interactable = adding ? keyInput.text.Length > 0 && !exists : valueInput.text != original;
                notice.SetText(exists ? "That key already exists." : EditorNotice);
            }

            void Finish()
            {
                builder.Handle.Close();
                Refresh();
            }
        }

        private static TMP_InputField AddInput(CustomUIBuilder builder, string placeholder, TMP_InputField.LineType lines)
        {
            builder.AddInput(placeholder);
            var input = builder.Last.SetPreferredSize(-1, 36).SetFlexible(1, 0).input;
            input.contentType = TMP_InputField.ContentType.Standard;
            input.lineType = lines;
            input.lineLimit = lines == TMP_InputField.LineType.SingleLine ? 1 : 0;
            input.characterLimit = 0;
            input.richText = false;
            return input;
        }

        private static CustomUIElement AddButton(CustomUIBuilder builder, string text, Action action)
        {
            builder.AddButton(text, action);
            return builder.Last.SetPreferredSize(100, 34).SetFlexible(0, 0);
        }

        private static string Preview(string value)
        {
            if (string.IsNullOrEmpty(value)) return "(empty)";
            int length = Math.Min(PreviewLength, value.Length);
            if (length < value.Length && char.IsHighSurrogate(value[length - 1])) length--;
            string prefix = value.Substring(0, length).Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
            return length < value.Length ? prefix + "…" : prefix;
        }
    }
}
