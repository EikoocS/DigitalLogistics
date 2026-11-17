using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using DigitalLogistics.Collections;
using static DigitalLogistics.Storage.StorageSort;

namespace DigitalLogistics.Filtering
{
    public static class StoredItemExpressionAnalyzer
    {
        /// <summary>False proves that the filters cannot match the same item; true also includes unknown cases.</summary>
        public static bool MayOverlap(StoredItemExpression first, StoredItemExpression second) =>
            MayMatch(first, second, null);

        /// <summary>False proves absence; true also includes cases that the analyzer cannot decide.</summary>
        /// <param name="mayContainString">Receives Identifier, ModId or DisplayName and an exact value. Return true for unindexed fields, false only for definite absence.</param>
        public static bool MayMatch(StoredItemExpression expression, Func<string, string, bool> mayContainString) =>
            Satisfy(expression.Tree.Body, true, new FilterConstraints())
                .Any(constraints => constraints.MayMatch(mayContainString));

        /// <summary>Analyzes both filters together before checking their surviving string constraints against the index.</summary>
        /// <param name="mayContainString">Receives Identifier, ModId or DisplayName and an exact value. Return true for unindexed fields, false only for definite absence.</param>
        public static bool MayMatch(StoredItemExpression providerFilter, StoredItemExpression searchFilter,
            Func<string, string, bool> mayContainString) =>
            Both(providerFilter.Tree.Body, true, searchFilter.Tree.Body, true, new FilterConstraints())
                .Any(constraints => constraints.MayMatch(mayContainString));

        internal static bool MayMatch(IReadOnlyList<StoredItemExpression> expressions,
            IReadOnlyDictionary<SortBy, BloomFilter> blooms, BloomFilter typeBloom) =>
            SatisfyAll(expressions, 0, new FilterConstraints())
                .Any(constraints => constraints.MayMatch(blooms, typeBloom));

        private static IEnumerable<FilterConstraints> SatisfyAll(IReadOnlyList<StoredItemExpression> expressions,
            int index, FilterConstraints constraints)
        {
            if (index == expressions.Count)
            {
                yield return constraints;
                yield break;
            }
            foreach (var next in Satisfy(expressions[index].Tree.Body, true, constraints))
                foreach (var result in SatisfyAll(expressions, index + 1, next)) yield return result;
        }

        private static IEnumerable<FilterConstraints> Satisfy(Expression expression, bool expected,
            FilterConstraints constraints)
        {
            if (expression is ConstantExpression constant && constant.Value is bool boolean)
            {
                if (boolean == expected) yield return constraints;
                yield break;
            }
            if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Not)
            {
                foreach (var result in Satisfy(unary.Operand, !expected, constraints)) yield return result;
                yield break;
            }
            if (expression is BinaryExpression binary)
            {
                IEnumerable<FilterConstraints> combinations = null;
                switch (binary.NodeType)
                {
                    case ExpressionType.AndAlso:
                        combinations = expected
                            ? Both(binary.Left, true, binary.Right, true, constraints)
                            : Either(binary.Left, false, binary.Right, false, constraints);
                        break;
                    case ExpressionType.OrElse:
                        combinations = expected
                            ? Either(binary.Left, true, binary.Right, true, constraints)
                            : Both(binary.Left, false, binary.Right, false, constraints);
                        break;
                    case ExpressionType.ExclusiveOr:
                        combinations = BooleanPair(binary.Left, binary.Right, !expected, constraints);
                        break;
                    case ExpressionType.Equal when binary.Left.Type == typeof(bool):
                        combinations = BooleanPair(binary.Left, binary.Right, expected, constraints);
                        break;
                }
                if (combinations != null)
                {
                    foreach (var result in combinations) yield return result;
                    yield break;
                }
            }

            var next = constraints.WithAtom(expression, expected);
            if (next != null && ApplyComparison(expression, expected, next)) yield return next;
        }

        private static IEnumerable<FilterConstraints> Both(Expression first, bool firstExpected,
            Expression second, bool secondExpected, FilterConstraints constraints)
        {
            foreach (var left in Satisfy(first, firstExpected, constraints))
                foreach (var right in Satisfy(second, secondExpected, left)) yield return right;
        }

        private static IEnumerable<FilterConstraints> Either(Expression first, bool firstExpected,
            Expression second, bool secondExpected, FilterConstraints constraints)
        {
            foreach (var result in Satisfy(first, firstExpected, constraints)) yield return result;
            foreach (var result in Satisfy(second, secondExpected, constraints)) yield return result;
        }

        private static IEnumerable<FilterConstraints> BooleanPair(Expression first, Expression second,
            bool equal, FilterConstraints constraints)
        {
            foreach (var result in Both(first, true, second, equal, constraints)) yield return result;
            foreach (var result in Both(first, false, second, !equal, constraints)) yield return result;
        }

        private static bool ApplyComparison(Expression expression, bool expected, FilterConstraints constraints)
        {
            if (FilterValueReference.TryRead(expression, out var booleanReference)
                && booleanReference.Type == typeof(bool))
                return constraints.AddEquality(booleanReference, expected, true);

            if (!(expression is BinaryExpression binary) || !IsComparison(binary.NodeType)) return true;
            Expression left = binary.Left;
            Expression right = binary.Right;
            ExpressionType comparison = binary.NodeType;

            bool leftConstant = FilterValueReference.TryReadConstant(left, out object leftValue);
            bool rightConstant = FilterValueReference.TryReadConstant(right, out object value);
            if (leftConstant && rightConstant)
            {
                if (FilterValueReference.IsInteger(left.Type) && FilterValueReference.IsInteger(right.Type))
                    return Compare(comparison, Convert.ToDecimal(leftValue), Convert.ToDecimal(value)) == expected;
                if (comparison == ExpressionType.Equal && (left.Type == typeof(string) || left.Type == typeof(bool)))
                    return Equals(leftValue, value) == expected;
            }
            if (leftConstant && !rightConstant)
            {
                left = binary.Right;
                right = binary.Left;
                value = leftValue;
                rightConstant = true;
                comparison = Reverse(comparison);
            }
            if (!rightConstant || !FilterValueReference.TryRead(left, out var reference)) return true;

            if (FilterValueReference.IsInteger(left.Type) && FilterValueReference.IsInteger(right.Type))
                return constraints.AddInteger(reference, expected ? comparison : Negate(comparison),
                    Convert.ToDecimal(value));
            if (comparison == ExpressionType.Equal && reference.Type == typeof(string))
                return constraints.AddEquality(reference, value, expected);
            return true;
        }

        private static bool IsComparison(ExpressionType type) => type == ExpressionType.Equal
            || type == ExpressionType.GreaterThan || type == ExpressionType.GreaterThanOrEqual
            || type == ExpressionType.LessThan || type == ExpressionType.LessThanOrEqual;

        private static ExpressionType Reverse(ExpressionType type) => type switch
        {
            ExpressionType.GreaterThan => ExpressionType.LessThan,
            ExpressionType.GreaterThanOrEqual => ExpressionType.LessThanOrEqual,
            ExpressionType.LessThan => ExpressionType.GreaterThan,
            ExpressionType.LessThanOrEqual => ExpressionType.GreaterThanOrEqual,
            _ => type
        };

        private static ExpressionType Negate(ExpressionType type) => type switch
        {
            ExpressionType.Equal => ExpressionType.NotEqual,
            ExpressionType.GreaterThan => ExpressionType.LessThanOrEqual,
            ExpressionType.GreaterThanOrEqual => ExpressionType.LessThan,
            ExpressionType.LessThan => ExpressionType.GreaterThanOrEqual,
            ExpressionType.LessThanOrEqual => ExpressionType.GreaterThan,
            _ => type
        };

        private static bool Compare(ExpressionType type, decimal left, decimal right) => type switch
        {
            ExpressionType.Equal => left == right,
            ExpressionType.GreaterThan => left > right,
            ExpressionType.GreaterThanOrEqual => left >= right,
            ExpressionType.LessThan => left < right,
            ExpressionType.LessThanOrEqual => left <= right,
            _ => false
        };
    }
}
