using System;
using System.Linq;

namespace DigitalLogistics.Storage
{
    public readonly struct FluidTransferResult
    {
        public int RemovedUL { get; }
        public int ReceivedUL { get; }
        internal FluidTransferResult(int removedUL, int receivedUL)
        {
            RemovedUL = removedUL;
            ReceivedUL = receivedUL;
        }
    }

    public static class FluidTransfer
    {
        public static FluidTransferResult Transfer(FluidStorageProvider source, FluidStorageProvider target,
            int volumeUL, bool applyTargetFilter = true)
        {
            if (source == target || !source.Available || !target.Available
                || applyTargetFilter && !target.Filter.Evaluate(source.Content)) return default;
            int amount = Math.Min(volumeUL, Math.Min(source.VolumeUL, target.FreeCapacityUL));
            if (amount <= 0) return default;
            var sourceItem = source.PhysicalContainer;
            var targetItem = target.PhysicalContainer;
            if (sourceItem != null && sourceItem == targetItem) return default;
            if (sourceItem != null && targetItem != null
                && amount == Math.Min(source.VolumeUL, target.FreeCapacityUL))
            {
                int beforeSource = source.VolumeUL;
                int beforeTarget = target.VolumeUL;
                WaterHelper.TransferLiquid(sourceItem, targetItem);
                source.Synchronize();
                target.Synchronize();
                var result = new FluidTransferResult(beforeSource - source.VolumeUL, target.VolumeUL - beforeTarget);
                source.Commit();
                target.Commit();
                return result;
            }

            bool wasEmpty = targetItem != null && WaterHelper.IsEmpty(targetItem);
            double temperature = source.Temperature;
            var sourceParts = source.ReadParts();
            var targetParts = target.ReadParts();
            var removed = WaterHelper.SplitProportionally(sourceParts, amount);
            var received = (int[])removed.Clone();
            var filter = targetItem == null ? null : WaterHelper.GetFilterItem(targetItem);
            if (filter != null)
                for (int i = 0; i < received.Length; i++)
                    received[i] = WaterHelper.HandleFilter(Liquid.Liquids[i].identifier, received[i], sourceItem, filter);
            int receivedUL = received.Sum();
            target.ReceiveTemperature(receivedUL, temperature);
            for (int i = 0; i < removed.Length; i++)
            {
                source.SetPart(i, sourceParts[i] - removed[i]);
                target.SetPart(i, targetParts[i] + received[i]);
            }
            source.Synchronize();
            target.Synchronize();
            if (targetItem != null)
            {
                if (sourceItem != null) WaterFeatureHelper.OnLiquidContainerPourInto(targetItem, sourceItem, wasEmpty);
                else if (!targetItem.IsTag("WINE_CONTAINER_TAG"))
                    WaterFeatureHelper.OnLiquidChanged(targetItem, exposeFeature: false);
            }
            if (sourceItem != null) WaterFeatureHelper.OnLiquidContainerPourOutOf(sourceItem);
            source.Commit();
            target.Commit();
            return new FluidTransferResult(removed.Sum(), receivedUL);
        }
    }
}
