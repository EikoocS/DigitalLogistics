namespace DigitalLogistics.Tracing
{
    internal sealed class SearchMeasurement
    {
        private readonly PerformanceTrace capture;
        private readonly TraceTimer response = new(true);
        private TraceTimer listWork = new(true, start: false);
        private long lookupTicks;

        private SearchMeasurement(PerformanceTrace capture) => this.capture = capture;

        internal static SearchMeasurement Begin() =>
            PerformanceTrace.Active is { } capture ? new SearchMeasurement(capture) : null;

        internal void AddLookup(long ticks) => lookupTicks += ticks;
        internal void BeginListWork() => listWork.Resume();
        internal void EndListWork() => listWork.Pause();

        internal void Record(SearchOutcome outcome, int resultCount) =>
            capture.RecordSearch(outcome, lookupTicks, listWork.ElapsedTicks, response.ElapsedTicks, resultCount);
    }
}
