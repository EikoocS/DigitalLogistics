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
        public int Count = 1;
        internal int RepeatCount => Math.Max(1, Count);
    }

    [Serializable]
    internal sealed class IOBusScheduleEntry : BusScheduleEntry
    {
        public string Filter = string.Empty;
        [NonSerialized] private StoredItemExpression expression;
        internal StoredItemExpression Expression => string.IsNullOrWhiteSpace(Filter)
            ? null : expression ??= StoredItemExpressionParser.Parse(Filter);

        internal void SetFilter(string filter)
        {
            Filter = filter;
            expression = null;
        }
    }
}
