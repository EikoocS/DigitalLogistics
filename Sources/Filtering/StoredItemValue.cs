using System;
using System.Linq.Expressions;

namespace DigitalLogistics.Filtering
{
    public class StoredItemValue<T>
    {
        internal Expression Body { get; }

        internal StoredItemValue(Expression body) => Body = body;

        public CachedItemExpression EqualTo(T value) =>
            Compare(ExpressionType.Equal, Expression.Constant(value, typeof(T)));

        public CachedItemExpression EqualTo(StoredItemValue<T> other) =>
            Compare(ExpressionType.Equal, other.Body);

        private protected CachedItemExpression Compare(ExpressionType op, Expression other) =>
            new(FilterExpressionBuilder.Binary(op, Body, other));
    }

    public sealed class StoredItemNumber<T> : StoredItemValue<T> where T : struct
    {
        internal StoredItemNumber(Expression body) : base(body) { }

        public CachedItemExpression GreaterThan(T value) =>
            Compare(ExpressionType.GreaterThan, Expression.Constant(value, typeof(T)));
        public CachedItemExpression GreaterThan(StoredItemNumber<T> other) =>
            Compare(ExpressionType.GreaterThan, other.Body);

        public CachedItemExpression GreaterThanOrEqual(T value) =>
            Compare(ExpressionType.GreaterThanOrEqual, Expression.Constant(value, typeof(T)));
        public CachedItemExpression GreaterThanOrEqual(StoredItemNumber<T> other) =>
            Compare(ExpressionType.GreaterThanOrEqual, other.Body);

        public CachedItemExpression LessThan(T value) =>
            Compare(ExpressionType.LessThan, Expression.Constant(value, typeof(T)));
        public CachedItemExpression LessThan(StoredItemNumber<T> other) =>
            Compare(ExpressionType.LessThan, other.Body);

        public CachedItemExpression LessThanOrEqual(T value) =>
            Compare(ExpressionType.LessThanOrEqual, Expression.Constant(value, typeof(T)));
        public CachedItemExpression LessThanOrEqual(StoredItemNumber<T> other) =>
            Compare(ExpressionType.LessThanOrEqual, other.Body);
    }

    public sealed class StoredItemString : StoredItemValue<string>
    {
        internal StoredItemString(Expression body) : base(body) { }

        public CachedItemExpression Contains(string value) => new(
            FilterExpressionBuilder.Contains(Body, Expression.Constant(value, typeof(string))));

        public CachedItemExpression Contains(string value, StringComparison comparison) => new(
            FilterExpressionBuilder.Contains(Body, Expression.Constant(value, typeof(string)), comparison));

        public CachedItemExpression Matches(string pattern) => new(
            FilterExpressionBuilder.Matches(Body, Expression.Constant(pattern, typeof(string))));
    }
}
