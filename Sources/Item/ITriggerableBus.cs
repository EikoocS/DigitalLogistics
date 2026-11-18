using System.Collections.Generic;
using DigitalLogistics.Buses;

namespace DigitalLogistics.Item
{
    internal interface ITriggerableBus
    {
        IEnumerable<BusOperation> GetOperations(bool nightly);
    }
}
