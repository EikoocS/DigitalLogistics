using System;
using System.Globalization;

namespace DigitalLogistics.Buses
{
    internal enum FluidVolumeUnit { UL, ML, L }

    [Serializable]
    internal sealed class FluidVolume
    {
        public int VolumeUL = 1000;
        public FluidVolumeUnit Unit = FluidVolumeUnit.ML;
        internal static readonly string[] Units = { "ul", "ml", "L" };
        private int Factor => Unit switch { FluidVolumeUnit.UL => 1, FluidVolumeUnit.ML => 1000, _ => 1000000 };
        internal string DisplayValue => (VolumeUL / (decimal)Factor).ToString(CultureInfo.InvariantCulture);

        internal bool SetValue(string text)
        {
            if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
                || value < 0 || value > int.MaxValue / (decimal)Factor) return false;
            decimal volume = value * Factor;
            if (volume != decimal.Truncate(volume)) return false;
            VolumeUL = (int)volume;
            return true;
        }
    }
}
