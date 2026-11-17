namespace DigitalLogistics
{
    internal sealed class ModConfiguration
    {
        internal readonly ConfigEntry<int> SearchDelayMilliseconds;
        internal readonly ConfigEntry<int> RefreshHoldMilliseconds;
        internal readonly ConfigEntry<bool> NotifyRefreshResult;
        internal readonly ConfigEntry<float> SearchBudgetMilliseconds;
        internal readonly ConfigEntry<float> TerminalBudgetMilliseconds;
        internal readonly ConfigEntry<float> IndexRefreshBudgetMilliseconds;
        internal readonly ConfigEntry<int> MaxContinuousTriggers;

        internal ModConfiguration(string modId)
        {
            var config = ModHelper.GetConfig(modId);
            SearchDelayMilliseconds = config.Define("search_delay_ms", 300, "Digital Logistics",
                () => Localize("search_delay_ms"));
            RefreshHoldMilliseconds = config.Define("refresh_hold_ms", 650, "Digital Logistics",
                () => Localize("refresh_hold_ms"));
            NotifyRefreshResult = config.Define("notify_refresh_result", true, "Digital Logistics",
                () => Localize("notify_refresh_result"));
            SearchBudgetMilliseconds = config.Define("search_budget_ms", 2f, "Digital Logistics",
                () => Localize("search_budget_ms"));
            TerminalBudgetMilliseconds = config.Define("terminal_budget_ms", 2f, "Digital Logistics",
                () => Localize("terminal_budget_ms"));
            IndexRefreshBudgetMilliseconds = config.Define("index_refresh_budget_ms", 2f, "Digital Logistics",
                () => Localize("index_refresh_budget_ms"));
            MaxContinuousTriggers = config.Define("max_continuous_triggers", 999, "Digital Logistics",
                () => Localize("max_continuous_triggers"));

            string Localize(string key) => ModHelper.GetLocalized(modId, "config." + key);
        }
    }
}
