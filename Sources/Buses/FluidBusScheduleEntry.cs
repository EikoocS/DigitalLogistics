using System;

namespace DigitalLogistics.Buses
{
    [Serializable]
    internal sealed class FluidBusScheduleEntry : FilteredBusScheduleEntry
    {
        public FluidVolume Amount = new();
    }
}
