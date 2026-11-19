using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Storage.Items;

namespace DigitalLogistics.Filtering
{
    internal static class CustomerItemFilter
    {
        internal static CachedItemExpression Build(StoreClient client)
        {
            var legalType = CachedItemExpression.ItemType(ItemTypeMask.MatchIndexFromString(TypeHelper.CONTRABAND)).Not();
            var expression = WantedTypes(client, legalType);
            var rejected = AnyType(client.clientBlackTagList);
            if (rejected != null) expression = expression.And(rejected.Not());
            bool security = client.isSecurity || client.IsFaction("FACTION_SECURITY");
            if (client.noAcceptingContraband || security) expression = expression.And(legalType);
            return security ? expression.And(CachedItemExpression.HasHeat.Not()) : expression;
        }

        private static CachedItemExpression WantedTypes(StoreClient client, CachedItemExpression legalType)
        {
            if (client.IsClientBuying())
            {
                var types = client.clientBuyingTagList;
                if (types.Contains(TypeHelper.ALL_TYPE)) return CachedItemExpression.Constant(true);
                if (types.Contains(TypeHelper.ALL_TYPE_EXCEPT_CONTRABAND)) return legalType;
                return AnyType(types) ?? CachedItemExpression.Constant(true);
            }
            if (client.barterOffer is { isFreeReward: false } barter && barter.UseCondition())
                return AnyType(barter.conditions.Select(condition => condition.requiredItemTag))
                    ?? CachedItemExpression.Constant(true);
            return CachedItemExpression.Constant(false);
        }

        private static CachedItemExpression AnyType(IEnumerable<string> types)
        {
            CachedItemExpression expression = null;
            foreach (var name in types)
            {
                int index = ItemTypeMask.MatchIndexFromString(name);
                if (index < 0) continue;
                var type = CachedItemExpression.ItemType(index);
                expression = expression?.Or(type) ?? type;
            }
            return expression;
        }
    }
}
