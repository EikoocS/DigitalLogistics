using DigitalLogistics.Filtering;
using DigitalLogistics.Item;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DigitalLogistics.Buses
{
    [JsonConverter(typeof(StringEnumConverter))]
    internal enum BusMode { External, Internal, Disabled }

    internal sealed class BusGlobalConfiguration
    {
        public BusMode Mode;
    }

    internal abstract class BusTargetConfiguration
    {
        public bool Disabled;
    }

    internal sealed class StorageBusConfiguration : BusTargetConfiguration
    {
        internal const string DefaultFilter = "True";
        public string Name = string.Empty;
        public int Priority;
        public string Filter = string.Empty;
    }

    internal class TransferBusConfiguration : BusTargetConfiguration
    {
        public TransferBusConfiguration() => Disabled = true;

        public string Name = string.Empty;
        public string Filter = string.Empty;
        private string parsedFilter;
        private CachedItemExpression expression;

        internal CachedItemExpression GetExpression(string defaultFilter)
        {
            string filter = string.IsNullOrWhiteSpace(Filter) ? defaultFilter : Filter;
            if (filter != parsedFilter)
            {
                expression = CachedItemExpressionParser.Parse(filter);
                parsedFilter = filter;
            }
            return expression;
        }
    }

    internal sealed class FluidTransferBusConfiguration : TransferBusConfiguration
    {
        public bool AvoidMixing = true;
    }

    internal static class BusActions
    {
        internal const string ItemStorage = "item.store";
        internal const string FluidStorage = "fluid.store";

        internal static string ForTransfer(GameItem bus) => bus.identifier switch
        {
            IOBus.InputId => "item.input",
            IOBus.OutputId => "item.output",
            IOBus.InputOutputId => "item.input_output",
            FluidIOBus.InputId => "fluid.input",
            FluidIOBus.OutputId => "fluid.output",
            FluidIOBus.InputOutputId => "fluid.input_output",
            _ => throw new System.ArgumentOutOfRangeException(nameof(bus), bus.identifier, null)
        };
    }
}
