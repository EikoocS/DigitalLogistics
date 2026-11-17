using System;
using System.Collections.Generic;

namespace DigitalLogistics.Item
{
    internal interface ITriggerableBus
    {
        IEnumerable<BusOperation> GetOperations(bool nightly);
    }

    internal enum BusOperationKind
    {
        Output,
        Trigger,
        Input
    }

    internal readonly struct BusOperation
    {
        internal int Order { get; }
        internal int Priority { get; }
        internal BusOperationKind Kind { get; }
        internal Action Execute { get; }

        internal BusOperation(int order, int priority, BusOperationKind kind, Action execute)
        {
            Order = order;
            Priority = priority;
            Kind = kind;
            Execute = execute;
        }
    }
}
