using System;
using System.Collections.Generic;
using DigitalLogistics.Buses;

namespace DigitalLogistics.Tracing
{
    internal enum BusRunSource { Manual, Automatic, Nightly }
    internal enum SearchOutcome { Completed, Cancelled, Failed }

    internal struct TimingStats
    {
        internal long Count { get; private set; }
        internal long TotalTicks { get; private set; }
        internal long MaxTicks { get; private set; }
        internal double AverageTicks => (double)TotalTicks / Count;

        internal void Add(long ticks)
        {
            Count++;
            TotalTicks += ticks;
            MaxTicks = Math.Max(MaxTicks, ticks);
        }
    }

    internal readonly struct DiskPayloadSize
    {
        internal readonly long JsonBytes;
        internal readonly long GZipBytes;
        internal readonly int Base64Bytes;
        internal double GZipSavings => 1d - (double)GZipBytes / JsonBytes;
        internal double Base64Savings => 1d - (double)Base64Bytes / JsonBytes;

        internal DiskPayloadSize(long jsonBytes, long gzipBytes, int base64Bytes)
        {
            JsonBytes = jsonBytes;
            GZipBytes = gzipBytes;
            Base64Bytes = base64Bytes;
        }
    }

    internal sealed class PerformanceTrace
    {
        internal static PerformanceTrace Latest { get; private set; }
        internal static PerformanceTrace Active => Latest?.Recording == true ? Latest : null;

        private readonly Dictionary<BusRunSource, TimingStats> busBatches = new();
        private readonly Dictionary<(string Type, BusOperationKind Kind), (TimingStats Time, int SlowestBusId)> busOperations = new();
        private readonly Dictionary<SearchOutcome, (TimingStats Lookup, TimingStats List, TimingStats Response, long ResultCount)> searches = new();
        private readonly Dictionary<Guid, (DiskPayloadSize Size, long EncodeTicks)> disks = new();
        private TimingStats driveConnections;

        internal bool Recording { get; private set; } = true;
        internal IReadOnlyDictionary<BusRunSource, TimingStats> BusBatches => busBatches;
        internal IReadOnlyDictionary<(string Type, BusOperationKind Kind), (TimingStats Time, int SlowestBusId)> BusOperations => busOperations;
        internal IReadOnlyDictionary<SearchOutcome, (TimingStats Lookup, TimingStats List, TimingStats Response, long ResultCount)> Searches => searches;
        internal IReadOnlyDictionary<Guid, (DiskPayloadSize Size, long EncodeTicks)> Disks => disks;
        internal TimingStats DriveConnections => driveConnections;

        private PerformanceTrace() { }

        internal static void Start()
        {
            if (Active == null) Latest = new PerformanceTrace();
        }

        internal static void Stop()
        {
            if (Latest != null) Latest.Recording = false;
        }

        internal static void Clear()
        {
            bool wasRecording = Active != null;
            Reset();
            if (wasRecording) Start();
        }

        internal static void Reset()
        {
            Stop();
            Latest = null;
        }

        internal void RecordBatch(BusRunSource source, long elapsedTicks)
        {
            if (!Recording) return;
            busBatches.TryGetValue(source, out var stats);
            stats.Add(elapsedTicks);
            busBatches[source] = stats;
        }

        internal void RecordOperation(string type, BusOperationKind kind, int busId, long elapsedTicks)
        {
            if (!Recording) return;
            var key = (type, kind);
            busOperations.TryGetValue(key, out var stats);
            if (stats.Time.Count == 0 || elapsedTicks > stats.Time.MaxTicks) stats.SlowestBusId = busId;
            stats.Time.Add(elapsedTicks);
            busOperations[key] = stats;
        }

        internal void RecordSearch(SearchOutcome outcome, long lookupTicks, long listTicks, long responseTicks, int resultCount)
        {
            if (!Recording) return;
            searches.TryGetValue(outcome, out var stats);
            stats.Lookup.Add(lookupTicks);
            stats.List.Add(listTicks);
            stats.Response.Add(responseTicks);
            stats.ResultCount += resultCount;
            searches[outcome] = stats;
        }

        internal void RecordDisk(Guid id, DiskPayloadSize size, long encodeTicks)
        {
            if (Recording) disks[id] = (size, encodeTicks);
        }

        internal void RecordDriveConnection(long elapsedTicks)
        {
            if (Recording) driveConnections.Add(elapsedTicks);
        }
    }
}
