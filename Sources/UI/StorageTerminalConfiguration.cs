using System;
using DigitalStorage.Item;
using DigitalStorage.Storage;

namespace DigitalStorage.UI
{
    internal sealed class StorageTerminalConfiguration
    {
        internal enum SafetyFilter { All, Legal, Illegal }

        private static readonly StorageSort.SortBy[] SortOptions =
            (StorageSort.SortBy[])Enum.GetValues(typeof(StorageSort.SortBy));
        private readonly MachineryButton sortButton;
        private readonly MachineryButton directionButton;
        private readonly MachineryButton safetyButton;

        internal MachineryStack Layout { get; }
        internal StorageSort.SortBy SortBy { get; private set; } = StorageSort.SortBy.IDENTIFIER;
        internal bool Reverse { get; private set; } = false;
        internal SafetyFilter Safety { get; private set; }
        internal event Action Changed;

        internal StorageTerminalConfiguration()
        {
            sortButton = CreateButton(() =>
            {
                SortBy = SortOptions[(Array.IndexOf(SortOptions, SortBy) + 1) % SortOptions.Length];
                OnChanged();
            });
            directionButton = CreateButton(() =>
            {
                Reverse = !Reverse;
                OnChanged();
            });
            safetyButton = CreateButton(() =>
            {
                Safety = (SafetyFilter)(((int)Safety + 1) % 3);
                OnChanged();
            });
            Layout = MachineryStack.Vertical(sortButton, directionButton, safetyButton)
                .SetAlign(MachineryStack.Align.Start)
                .SetMainAlign(MachineryStack.Align.Start);
            RefreshDescriptions();
        }

        internal void RefreshDescriptions()
        {
            SetOption(sortButton, "sort_by", SortBy.ToString().ToLowerInvariant());
            SetOption(directionButton, "sort_direction", Reverse ? "descending" : "ascending");
            string safety = Safety switch
            {
                SafetyFilter.Legal => "legal",
                SafetyFilter.Illegal => "illegal",
                _ => "all"
            };
            SetOption(safetyButton, "safety", safety);
        }

        internal void SetEnabled(bool enabled)
        {
            sortButton.SetDisabled(!enabled);
            directionButton.SetDisabled(!enabled);
            safetyButton.SetDisabled(!enabled);
        }

        private void OnChanged()
        {
            RefreshDescriptions();
            Changed?.Invoke();
        }

        private static string Localize(string key) => StorageTerminal.Localize("ui.storage_terminal." + key);

        private static void SetOption(MachineryButton button, string setting, string option)
        {
            button.SetDescription(string.Format(Localize(setting), Localize(option)));
            button.SetIcon(CustomUIManager.Instance.GetSprite(
                $"{DigitalStorage.Instance.Manifest.ID}:storage_terminal_{setting}_{option}"));
        }

        private static MachineryButton CreateButton(Action callback) =>
            new MachineryButton(callback: callback, width: 32, height: 24).SetFillWidth(false);
    }
}
