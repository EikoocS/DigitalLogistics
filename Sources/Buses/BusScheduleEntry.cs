using System;
using DigitalLogistics.Filtering;

namespace DigitalLogistics.Buses
{
    [Serializable]
    internal class BusScheduleEntry
    {
        public bool Enabled = true;
        public int Order = 6;
        public int Priority;
    }

    [Serializable]
    internal class CountedBusScheduleEntry : BusScheduleEntry
    {
        public int Count = 1;
        internal int RepeatCount => Math.Max(1, Count);
    }

    [Serializable]
    internal sealed class IOBusScheduleEntry : CountedBusScheduleEntry
    {
        public bool UnlimitedTransfer;
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
}
