using System;
using DigitalLogistics.Filtering;

namespace DigitalLogistics.Buses
{
    [Serializable]
    internal sealed class FluidBusScheduleEntry : BusScheduleEntry
    {
        public FluidVolume Amount = new();
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
