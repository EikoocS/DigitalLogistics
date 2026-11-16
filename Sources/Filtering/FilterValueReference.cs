using System;
using System.Linq.Expressions;
using DigitalStorage.Storage;
using static DigitalStorage.Storage.StorageSort;

namespace DigitalStorage.Filtering
{
    internal readonly struct FilterValueReference : IEquatable<FilterValueReference>
    {
        public string Getter { get; }
        public object Key { get; }
        public Type Type { get; }

        private FilterValueReference(string getter, object key, Type type)
        {
            Getter = getter;
            Key = key;
            Type = type;
        }

        public string StringProperty => Getter switch
        {
            nameof(StoredItemValueResolver.ReadIdentifier) => nameof(StoredItem.Identifier),
            nameof(StoredItemValueResolver.ReadModId) => nameof(StoredItem.ModId),
            nameof(StoredItemValueResolver.ReadDisplayName) => nameof(StoredItem.DisplayName),
            _ => null
        };

        public SortBy? SortField => Getter switch
        {
            nameof(StoredItemValueResolver.ReadIdentifier) => SortBy.IDENTIFIER,
            nameof(StoredItemValueResolver.ReadModId) => SortBy.MOD_ID,
            nameof(StoredItemValueResolver.ReadDisplayName) => SortBy.DISPLAY_NAME,
            nameof(StoredItemValueResolver.ReadPrice) => SortBy.PRICE,
            nameof(StoredItemValueResolver.ReadBasePrice) => SortBy.BASE_PRICE,
            nameof(StoredItemValueResolver.ReadHeat) => SortBy.HEAT,
            _ => null
        };

        public static bool TryRead(Expression expression, out FilterValueReference reference)
        {
            if (expression is UnaryExpression conversion && IsIntegerWidening(conversion))
                expression = conversion.Operand;

            if (expression is MethodCallExpression call
                && call.Method.DeclaringType == typeof(StoredItemValueResolver)
                && call.Arguments.Count >= 1
                && call.Arguments[0] is ParameterExpression parameter
                && parameter.Type == typeof(StoredItem)
                && (call.Type == typeof(string) || call.Type == typeof(bool) || IsInteger(call.Type)))
            {
                object key = null;
                if (call.Arguments.Count == 2)
                {
                    if (!(call.Arguments[1] is ConstantExpression argument))
                    {
                        reference = default;
                        return false;
                    }
                    key = argument.Value;
                }
                reference = new FilterValueReference(call.Method.Name, key, call.Type);
                return true;
            }

            reference = default;
            return false;
        }

        public static bool TryReadConstant(Expression expression, out object value)
        {
            if (expression is ConstantExpression constant)
            {
                value = constant.Value;
                return true;
            }

            if (expression is UnaryExpression unary
                && TryReadConstant(unary.Operand, out object operand))
            {
                if (IsIntegerWidening(unary))
                {
                    value = (long)(int)operand;
                    return true;
                }
                if (unary.NodeType == ExpressionType.Negate)
                {
                    if (operand is int integer)
                    {
                        value = unchecked(-integer);
                        return true;
                    }
                    if (operand is long large)
                    {
                        value = unchecked(-large);
                        return true;
                    }
                }
            }

            value = null;
            return false;
        }

        public static bool IsInteger(Type type) => type == typeof(int) || type == typeof(long);

        private static bool IsIntegerWidening(UnaryExpression expression) =>
            expression.NodeType == ExpressionType.Convert
            && expression.Operand.Type == typeof(int) && expression.Type == typeof(long);

        public bool Equals(FilterValueReference other) => Getter == other.Getter && Equals(Key, other.Key);
        public override bool Equals(object obj) => obj is FilterValueReference other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Getter, Key);
    }
}
