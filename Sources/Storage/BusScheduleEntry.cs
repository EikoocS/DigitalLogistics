using System;

namespace DigitalLogistics.Storage
{
    [Serializable]
    internal sealed class BusScheduleEntry
    {
        public bool Enabled = true;
        public int Order = 6;
        public int Priority;
    }
}
