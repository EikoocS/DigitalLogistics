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
            sortButton = CreateButton("storage_terminal_sort_by", () =>
            {
                SortBy = SortOptions[(Array.IndexOf(SortOptions, SortBy) + 1) % SortOptions.Length];
                OnChanged();
            });
            directionButton = CreateButton("storage_terminal_sort_direction", () =>
            {
                Reverse = !Reverse;
                OnChanged();
            });
            safetyButton = CreateButton("storage_terminal_safety", () =>
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
            sortButton.SetDescription($"{Localize("sort_by")}: {SortBy}");
            directionButton.SetDescription($"{Localize("sort_direction")}: {Localize(Reverse ? "descending" : "ascending")}");
            string safety = Safety switch
            {
                SafetyFilter.Legal => "legal",
                SafetyFilter.Illegal => "illegal",
                _ => "all"
            };
            safetyButton.SetDescription($"{Localize("safety")}: {Localize(safety)}");
        }

        private void OnChanged()
        {
            RefreshDescriptions();
            Changed?.Invoke();
        }

        private static string Localize(string key) => StorageTerminal.Localize("ui.storage_terminal." + key);

        private static MachineryButton CreateButton(string spriteName, Action callback)
        {
            var sprite = CustomUIManager.Instance.GetSprite(DigitalStorage.Instance.Manifest.ID + ":" + spriteName);
            return new MachineryButton(sprite, sprite, sprite, sprite, callback).SetSize(24, 24);
        }
    }
}
