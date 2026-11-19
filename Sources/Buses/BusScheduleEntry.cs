using System;
using System.Collections.Generic;
using DigitalLogistics.Filtering;

namespace DigitalLogistics.Buses
{
    [Serializable]
    internal abstract class BusScheduleEntry
    {
        public bool Enabled = true;
        public int Order = 6;
        public int SubOrder;
        public bool Limit;
    }

    [Serializable]
    internal sealed class TriggerBusScheduleEntry : BusScheduleEntry
    {
        public int Count = 1;
    }

    [Serializable]
    internal abstract class FilteredBusScheduleEntry : BusScheduleEntry
    {
        public string Filter = string.Empty;
        [NonSerialized] private CachedItemExpression expression;
        internal CachedItemExpression Expression => string.IsNullOrWhiteSpace(Filter)
            ? null : expression ??= CachedItemExpressionParser.Parse(Filter);

        internal void SetFilter(string filter)
        {
            Filter = filter;
            expression = null;
        }
    }

    [Serializable]
    internal sealed class IOBusScheduleEntry : FilteredBusScheduleEntry
    {
        public int Count = 1;
    }

    [Serializable]
    internal class TransferBusConfiguration<T> where T : FilteredBusScheduleEntry
    {
        public string Key;
        public string Name = string.Empty;
        public bool Disabled = true;
        public string Filter = string.Empty;
        public List<T> Schedule = new();
    }
}
