using System;
using System.Linq.Expressions;

namespace DigitalStorage.Filtering
{
    public class StoredItemValue<T>
    {
        internal Expression Body { get; }

        internal StoredItemValue(Expression body) => Body = body;

        public StoredItemExpression EqualTo(T value) =>
            Compare(ExpressionType.Equal, Expression.Constant(value, typeof(T)));

        public StoredItemExpression EqualTo(StoredItemValue<T> other) =>
            Compare(ExpressionType.Equal, other.Body);

        private protected StoredItemExpression Compare(ExpressionType op, Expression other) =>
            new(FilterExpressionBuilder.Binary(op, Body, other));
    }

    public sealed class StoredItemNumber<T> : StoredItemValue<T> where T : struct
    {
        internal StoredItemNumber(Expression body) : base(body) { }

        public StoredItemExpression GreaterThan(T value) =>
            Compare(ExpressionType.GreaterThan, Expression.Constant(value, typeof(T)));
        public StoredItemExpression GreaterThan(StoredItemNumber<T> other) =>
            Compare(ExpressionType.GreaterThan, other.Body);

        public StoredItemExpression GreaterThanOrEqual(T value) =>
            Compare(ExpressionType.GreaterThanOrEqual, Expression.Constant(value, typeof(T)));
        public StoredItemExpression GreaterThanOrEqual(StoredItemNumber<T> other) =>
            Compare(ExpressionType.GreaterThanOrEqual, other.Body);

        public StoredItemExpression LessThan(T value) =>
            Compare(ExpressionType.LessThan, Expression.Constant(value, typeof(T)));
        public StoredItemExpression LessThan(StoredItemNumber<T> other) =>
            Compare(ExpressionType.LessThan, other.Body);

        public StoredItemExpression LessThanOrEqual(T value) =>
            Compare(ExpressionType.LessThanOrEqual, Expression.Constant(value, typeof(T)));
        public StoredItemExpression LessThanOrEqual(StoredItemNumber<T> other) =>
            Compare(ExpressionType.LessThanOrEqual, other.Body);
    }

    public sealed class StoredItemString : StoredItemValue<string>
    {
        internal StoredItemString(Expression body) : base(body) { }

        public StoredItemExpression Contains(string value) => new(
            FilterExpressionBuilder.Contains(Body, Expression.Constant(value, typeof(string))));

        public StoredItemExpression Contains(string value, StringComparison comparison) => new(
            FilterExpressionBuilder.Contains(Body, Expression.Constant(value, typeof(string)), comparison));

        public StoredItemExpression Matches(string pattern) => new(
            FilterExpressionBuilder.Matches(Body, Expression.Constant(pattern, typeof(string))));
    }
}
