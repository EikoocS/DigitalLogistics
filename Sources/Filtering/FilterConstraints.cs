using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using DigitalLogistics.Collections;
using static DigitalLogistics.Storage.StorageSort;

namespace DigitalLogistics.Filtering
{
    internal sealed class FilterConstraints
    {
        private readonly Dictionary<Expression, bool> atoms;
        private readonly Dictionary<FilterValueReference, EqualityConstraint> equalities;
        private readonly Dictionary<FilterValueReference, IntegerConstraint> integers;

        public FilterConstraints()
        {
            atoms = new(FilterExpressionComparer.Instance);
            equalities = new();
            integers = new();
        }

        private FilterConstraints(FilterConstraints source)
        {
            atoms = new(source.atoms, FilterExpressionComparer.Instance);
            equalities = new(source.equalities);
            integers = new(source.integers);
        }

        public FilterConstraints WithAtom(Expression expression, bool expected)
        {
            if (atoms.TryGetValue(expression, out bool existing) && existing != expected) return null;
            var result = new FilterConstraints(this);
            result.atoms[expression] = expected;
            return result;
        }

        public bool AddEquality(FilterValueReference reference, object value, bool equal)
        {
            var constraint = equalities.TryGetValue(reference, out var existing)
                ? new EqualityConstraint(existing) : new EqualityConstraint();
            equalities[reference] = constraint;
            return constraint.Add(value, equal);
        }

        public bool AddInteger(FilterValueReference reference, ExpressionType comparison, decimal value)
        {
            var constraint = integers.TryGetValue(reference, out var existing)
                ? new IntegerConstraint(existing) : new IntegerConstraint(reference.Type);
            integers[reference] = constraint;
            return constraint.Add(comparison, value);
        }

        public bool MayMatch(Func<string, string, bool> mayContainString)
        {
            if (mayContainString == null) return true;
            foreach (var entry in equalities)
            {
                string property = entry.Key.StringProperty;
                var constraint = entry.Value;
                if (property != null && constraint.HasValue
                    && !mayContainString(property, (string)constraint.Value)) return false;
            }
            return true;
        }

        public bool MayMatch(IReadOnlyDictionary<SortBy, BloomFilter> blooms, BloomFilter typeBloom)
        {
            foreach (var entry in equalities)
            {
                var constraint = entry.Value;
                if (!constraint.HasValue) continue;
                var reference = entry.Key;
                if (reference.SortField is SortBy field && blooms.TryGetValue(field, out var bloom)
                    && !bloom.MayContain(constraint.Value?.GetHashCode() ?? 0)) return false;
                if (typeBloom != null && reference.Getter == nameof(StoredItemValueResolver.ReadItemType)
                    && (bool)constraint.Value && !typeBloom.MayContain((int)reference.Key)) return false;
            }
            foreach (var entry in integers)
            {
                var reference = entry.Key;
                if (reference.SortField is SortBy field && blooms.TryGetValue(field, out var bloom)
                    && entry.Value.TryGetValue(out decimal value))
                {
                    int hash = reference.Type == typeof(int) ? ((int)value).GetHashCode() : ((long)value).GetHashCode();
                    if (!bloom.MayContain(hash)) return false;
                }
            }
            return true;
        }

        private sealed class EqualityConstraint
        {
            public bool HasValue { get; private set; }
            public object Value { get; private set; }
            private readonly HashSet<object> excluded;

            public EqualityConstraint() => excluded = new();

            public EqualityConstraint(EqualityConstraint source)
            {
                HasValue = source.HasValue;
                Value = source.Value;
                excluded = new(source.excluded);
            }

            public bool Add(object value, bool equal)
            {
                if (equal)
                {
                    if (HasValue && !Equals(Value, value) || excluded.Contains(value)) return false;
                    HasValue = true;
                    Value = value;
                }
                else
                {
                    if (HasValue && Equals(Value, value)) return false;
                    excluded.Add(value);
                }
                return true;
            }
        }

        private sealed class IntegerConstraint
        {
            private decimal minimum;
            private decimal maximum;
            private readonly HashSet<decimal> excluded;

            public IntegerConstraint(Type type)
            {
                minimum = type == typeof(int) ? int.MinValue : long.MinValue;
                maximum = type == typeof(int) ? int.MaxValue : long.MaxValue;
                excluded = new();
            }

            public IntegerConstraint(IntegerConstraint source)
            {
                minimum = source.minimum;
                maximum = source.maximum;
                excluded = new(source.excluded);
            }

            public bool TryGetValue(out decimal value)
            {
                value = minimum;
                return minimum == maximum;
            }

            public bool Add(ExpressionType comparison, decimal value)
            {
                switch (comparison)
                {
                    case ExpressionType.Equal:
                        minimum = Math.Max(minimum, value);
                        maximum = Math.Min(maximum, value);
                        break;
                    case ExpressionType.NotEqual: excluded.Add(value); break;
                    case ExpressionType.GreaterThan: minimum = Math.Max(minimum, value + 1); break;
                    case ExpressionType.GreaterThanOrEqual: minimum = Math.Max(minimum, value); break;
                    case ExpressionType.LessThan: maximum = Math.Min(maximum, value - 1); break;
                    case ExpressionType.LessThanOrEqual: maximum = Math.Min(maximum, value); break;
                }
                if (minimum > maximum) return false;
                decimal count = maximum - minimum + 1;
                if (count > excluded.Count) return true;
                int excludedCount = 0;
                foreach (decimal entry in excluded)
                    if (entry >= minimum && entry <= maximum) excludedCount++;
                return excludedCount < count;
            }
        }
    }
}
