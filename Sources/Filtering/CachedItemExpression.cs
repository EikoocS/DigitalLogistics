using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using DigitalLogistics.Storage.Items;

namespace DigitalLogistics.Filtering
{
    public sealed class CachedItemExpression
    {
        private readonly Lazy<Func<CachedItem, bool>> evaluate;

        public Expression<Func<CachedItem, bool>> Tree { get; }

        public static StoredItemString Identifier { get; } = new(
            FilterExpressionBuilder.Read(nameof(StoredItemValueResolver.ReadIdentifier)));
        public static StoredItemString ModId { get; } = new(
            FilterExpressionBuilder.Read(nameof(StoredItemValueResolver.ReadModId)));
        public static StoredItemString DisplayName { get; } = new(
            FilterExpressionBuilder.Read(nameof(StoredItemValueResolver.ReadDisplayName)));
        public static StoredItemNumber<long> Price { get; } = new(
            FilterExpressionBuilder.Read(nameof(StoredItemValueResolver.ReadPrice)));
        public static StoredItemNumber<long> BasePrice { get; } = new(
            FilterExpressionBuilder.Read(nameof(StoredItemValueResolver.ReadBasePrice)));
        public static StoredItemNumber<int> Heat { get; } = new(
            FilterExpressionBuilder.Read(nameof(StoredItemValueResolver.ReadHeat)));
        public static StoredItemNumber<int> ContrabandLevel { get; } = new(
            FilterExpressionBuilder.Read(nameof(StoredItemValueResolver.ReadContrabandLevel)));

        internal CachedItemExpression(Expression body)
        {
            Tree = Expression.Lambda<Func<CachedItem, bool>>(body, FilterExpressionBuilder.Item);
            evaluate = new Lazy<Func<CachedItem, bool>>(() => Tree.Compile());
        }

        internal static CachedItemExpression HasIds(IEnumerable<int> uniqueIds) => new(
            Expression.Call(Expression.Constant(new HashSet<int>(uniqueIds)),
                nameof(HashSet<int>.Contains), null,
                Expression.Property(FilterExpressionBuilder.Item, nameof(CachedItem.UniqueId))));

        public static CachedItemExpression Constant(bool value) => new(Expression.Constant(value));

        public static CachedItemExpression ItemType(int index) => new(
            FilterExpressionBuilder.Read(nameof(StoredItemValueResolver.ReadItemType), Expression.Constant(index)));

        public static StoredItemNumber<int> TagInt(string key) => new(
            ReadTag(nameof(StoredItemValueResolver.ReadInt), key));
        public static StoredItemNumber<long> TagLong(string key) => new(
            ReadTag(nameof(StoredItemValueResolver.ReadLong), key));
        public static StoredItemNumber<float> TagFloat(string key) => new(
            ReadTag(nameof(StoredItemValueResolver.ReadFloat), key));
        public static StoredItemNumber<double> TagDouble(string key) => new(
            ReadTag(nameof(StoredItemValueResolver.ReadDouble), key));
        public static CachedItemExpression TagBool(string key) => new(
            ReadTag(nameof(StoredItemValueResolver.ReadBool), key));
        public static StoredItemString TagString(string key) => new(
            ReadTag(nameof(StoredItemValueResolver.ReadString), key));

        public CachedItemExpression And(CachedItemExpression other) => new(
            FilterExpressionBuilder.Binary(ExpressionType.AndAlso, Tree.Body, other.Tree.Body));
        public CachedItemExpression Or(CachedItemExpression other) => new(
            FilterExpressionBuilder.Binary(ExpressionType.OrElse, Tree.Body, other.Tree.Body));
        public CachedItemExpression Xor(CachedItemExpression other) => new(
            FilterExpressionBuilder.Binary(ExpressionType.ExclusiveOr, Tree.Body, other.Tree.Body));
        public CachedItemExpression Not() => new(
            FilterExpressionBuilder.Unary(ExpressionType.Not, Tree.Body));

        public bool Evaluate(CachedItem item) => evaluate.Value(item);

        private static Expression ReadTag(string getter, string key) =>
            FilterExpressionBuilder.Read(getter, Expression.Constant(key, typeof(string)));
    }
}
