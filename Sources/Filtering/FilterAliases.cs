using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DigitalLogistics.Filtering
{
    internal static class FilterAliases
    {
        private static readonly Dictionary<string, (Expression Value, Expression PercentageBase)> aliases =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["volume"] = (Read(nameof(StoredItemValueResolver.ReadLiquidVolume)), LiquidCapacity()),
                ["free_volume"] = (Read(nameof(StoredItemValueResolver.ReadFreeLiquidVolume)), LiquidCapacity()),
                ["capacity"] = (LiquidCapacity(), null),
                ["power"] = Tag(nameof(StoredItemValueResolver.ReadInt), PowerHelper.POWER_SOURCE_ITEM_ENERGY,
                    Int(PowerHelper.POWER_SOURCE_ITEM_MAX_ENERGY)),
                ["power_max"] = Tag(nameof(StoredItemValueResolver.ReadInt), PowerHelper.POWER_SOURCE_ITEM_MAX_ENERGY),
                ["durability"] = Tag(nameof(StoredItemValueResolver.ReadInt), DurabilityHelper.DURABILITY_TAG,
                    Int(DurabilityHelper.MAX_DURABILITY_TAG)),
                ["durability_max"] = Tag(nameof(StoredItemValueResolver.ReadInt), DurabilityHelper.MAX_DURABILITY_TAG),
                ["uses"] = Tag(nameof(StoredItemValueResolver.ReadInt), UseCountHelper.CURRENT_USE_COUNT_INT,
                    Int(UseCountHelper.MAX_USE_COUNT_INT)),
                ["uses_max"] = Tag(nameof(StoredItemValueResolver.ReadInt), UseCountHelper.MAX_USE_COUNT_INT),
                ["temp"] = Tag(nameof(StoredItemValueResolver.ReadDouble), ChemicalHelper.CONTAINER_TEMP_TAG),
                ["freshness"] = Tag(nameof(StoredItemValueResolver.ReadInt), FoodItemHelper.FRESHNESS_TAG),
                ["calories"] = Tag(nameof(StoredItemValueResolver.ReadInt), FoodItemHelper.CALORIE_VALUE_TAG),
                ["weight"] = Tag(nameof(StoredItemValueResolver.ReadInt), GeneralHelper.WEIGHT_TAG),
                ["chem_purity"] = Tag(nameof(StoredItemValueResolver.ReadDouble),
                    ChemicalProductHelper.CHEMICAL_PRODUCT_PURITY_TAG, Expression.Constant(100.0)),
                ["ingot_purity"] = Tag(nameof(StoredItemValueResolver.ReadString), IngotPurityHelper.INGOT_PURITY_TAG),
                ["progress"] = Tag(nameof(StoredItemValueResolver.ReadInt), MachineryHelper.PROCESSING_CURRENT_PROGRESS_INT,
                    Int(MachineryHelper.PROCESSING_MAX_PROGRESS_INT)),
                ["owned"] = Tag(nameof(StoredItemValueResolver.ReadEnabled), GeneralHelper.IS_OWNED_TAG),
                ["locked"] = Tag(nameof(StoredItemValueResolver.ReadEnabled), GeneralHelper.LOCKED_TAG),
                ["stolen"] = Tag(nameof(StoredItemValueResolver.ReadEnabled), StolenHelper.STOLEN_TAG)
            };

        internal static Expression Resolve(string name)
        {
            if (aliases.TryGetValue(name, out var alias)) return alias.Value;
            foreach (var liquid in Liquid.Liquids)
                if (string.Equals(name, liquid.identifier, StringComparison.OrdinalIgnoreCase))
                    return LiquidAmount(liquid.currentPart);
            throw new ArgumentException("Unknown alias '[" + name + "]'.");
        }

        internal static Expression GetPercentageBase(MethodCallExpression value)
        {
            foreach (var alias in aliases.Values)
                if (FilterExpressionComparer.Instance.Equals(value, alias.Value)) return alias.PercentageBase;
            if ((value.Method.Name is nameof(StoredItemValueResolver.ReadLiquidAmount) or nameof(StoredItemValueResolver.ReadInt))
                && value.Arguments.Count == 2 && value.Arguments[1] is ConstantExpression key)
                foreach (var liquid in Liquid.Liquids)
                    if (Equals(key.Value, liquid.currentPart))
                        return Read(nameof(StoredItemValueResolver.ReadLiquidVolume));
            return null;
        }

        private static (Expression Value, Expression PercentageBase) Tag(string getter, string key,
            Expression percentageBase = null) =>
            (FilterExpressionBuilder.Read(getter, Expression.Constant(key)), percentageBase);

        private static Expression Read(string getter) => FilterExpressionBuilder.Read(getter);
        private static Expression Int(string key) => Tag(nameof(StoredItemValueResolver.ReadInt), key).Value;
        private static Expression LiquidAmount(string key) => Tag(nameof(StoredItemValueResolver.ReadLiquidAmount), key).Value;
        private static Expression LiquidCapacity() => LiquidAmount(WaterHelper.LIQUID_CONTAINER_CAPACITY);
    }
}
