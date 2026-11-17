using System;
using DigitalLogistics.Item;
using DigitalLogistics.Storage;

namespace DigitalLogistics.UI
{
    internal sealed class StorageTerminalConfiguration
    {
        internal enum SafetyFilter { All, Legal, Illegal }

        private const string SortByTag = "DIGITAL_LOGISTICS_TERMINAL_SORT_BY";
        private const string ReverseTag = "DIGITAL_LOGISTICS_TERMINAL_REVERSE";
        private const string SafetyTag = "DIGITAL_LOGISTICS_TERMINAL_SAFETY";
        private readonly GameItem item;
        private static readonly StorageSort.SortBy[] SortOptions =
            (StorageSort.SortBy[])Enum.GetValues(typeof(StorageSort.SortBy));
        private readonly MachineryButton sortButton;
        private readonly MachineryButton directionButton;
        private readonly MachineryButton safetyButton;

        internal MachineryStack Layout { get; }
        internal StorageSort.SortBy SortBy
        {
            get => (StorageSort.SortBy)item.GetTagReadonly(SortByTag).GetInt();
            private set => item.ModifyTag(SortByTag, state => state.Enable().SetInt((int)value));
        }
        internal bool Reverse
        {
            get => item.GetTagReadonly(ReverseTag).GetBool();
            private set => item.ModifyTag(ReverseTag, state => state.Enable().SetBool(value));
        }
        internal SafetyFilter Safety
        {
            get => (SafetyFilter)item.GetTagReadonly(SafetyTag).GetInt();
            private set => item.ModifyTag(SafetyTag, state => state.Enable().SetInt((int)value));
        }
        internal event Action Changed;

        internal StorageTerminalConfiguration(GameItem item)
        {
            this.item = item;
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
                $"{DigitalLogistics.Instance.Manifest.ID}:storage_terminal_{setting}_{option}"));
        }

        private static MachineryButton CreateButton(Action callback) =>
            new MachineryButton(callback: callback, width: 32, height: 24).SetFillWidth(false);
    }
}
