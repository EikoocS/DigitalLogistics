namespace DigitalLogistics.UI.Buses
{
    internal interface IBusConfigurationRow
    {
        object Connection { get; }
        int Height { get; }
        void Build(BusRowControls controls);
    }
}
