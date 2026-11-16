using System;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using DigitalStorage.Storage;

namespace DigitalStorage.Filtering
{
    internal static class FilterExpressionBuilder
    {
        internal static readonly ParameterExpression Item = Expression.Parameter(typeof(StoredItem), "item");

        public static Expression Read(string getter) =>
            Expression.Call(typeof(StoredItemValueResolver), getter, null, Item);

        public static Expression Read(string getter, Expression key) =>
            Expression.Call(typeof(StoredItemValueResolver), getter, null, Item, key);

        public static Expression Unary(ExpressionType op, Expression operand)
        {
            if (op == ExpressionType.Not)
            {
                Require(operand.Type == typeof(bool), "NOT requires a boolean operand.");
                return Expression.Not(operand);
            }

            Require(IsNumeric(operand.Type), "Unary '+' and '-' require a numeric operand.");
            return op == ExpressionType.Negate ? Expression.Negate(operand) : operand;
        }

        public static Expression Binary(ExpressionType op, Expression left, Expression right)
        {
            switch (op)
            {
                case ExpressionType.AndAlso:
                case ExpressionType.OrElse:
                case ExpressionType.ExclusiveOr:
                    Require(left.Type == typeof(bool) && right.Type == typeof(bool),
                        "Boolean operators require boolean operands.");
                    return Expression.MakeBinary(op, left, right);

                case ExpressionType.Equal:
                    if (IsNumeric(left.Type) && IsNumeric(right.Type)) Promote(ref left, ref right);
                    Require(left.Type == right.Type, "Equality requires compatible operand types.");
                    return Expression.Equal(left, right);

                default:
                    Require(IsNumeric(left.Type) && IsNumeric(right.Type),
                        "Arithmetic and ordered comparisons require numeric operands.");
                    Promote(ref left, ref right);
                    return Expression.MakeBinary(op, left, right);
            }
        }

        public static Expression Contains(Expression value, Expression substring,
            StringComparison? comparison = null)
        {
            RequireStrings(value, substring);
            return comparison.HasValue
                ? Expression.Call(value, nameof(string.Contains), null, substring,
                    Expression.Constant(comparison.Value))
                : Expression.Call(value, nameof(string.Contains), null, substring);
        }

        public static Expression Matches(Expression value, Expression pattern)
        {
            RequireStrings(value, pattern);
            return Expression.Call(typeof(Regex), nameof(Regex.IsMatch), null, value, pattern);
        }

        private static void RequireStrings(Expression left, Expression right) =>
            Require(left.Type == typeof(string) && right.Type == typeof(string),
                "IN and MATCH require string operands.");

        private static bool IsNumeric(Type type) => NumericRank(type) >= 0;

        private static int NumericRank(Type type) =>
            type == typeof(int) ? 0 : type == typeof(long) ? 1 :
            type == typeof(float) ? 2 : type == typeof(double) ? 3 : -1;

        private static void Promote(ref Expression left, ref Expression right)
        {
            Type type = NumericRank(left.Type) >= NumericRank(right.Type) ? left.Type : right.Type;
            if (left.Type != type) left = Expression.Convert(left, type);
            if (right.Type != type) right = Expression.Convert(right, type);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new ArgumentException(message);
        }
    }
}
