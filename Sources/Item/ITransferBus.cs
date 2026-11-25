using System.Collections.Generic;

namespace DigitalLogistics.Item
{
    internal interface ITransferBus
    {
        IEnumerable<LogisticsScheduler.Binding> Transfers { get; }
    }
}
