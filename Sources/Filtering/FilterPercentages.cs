using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DigitalLogistics.Filtering
{
    internal static class FilterPercentages
    {
        private static readonly ParameterExpression percentageBase = Expression.Parameter(typeof(double), "percentageBase");

        internal static Expression Create(double percentage) =>
            Expression.Multiply(percentageBase, Expression.Constant(percentage / 100.0));

        internal static void Bind(ref Expression left, ref Expression right)
        {
            var context = new Context();
            context.Visit(left);
            context.Visit(right);
            if (!context.HasPercentage) return;

            Expression basis = null;
            foreach (var reference in context.References)
            {
                var candidate = FilterAliases.GetPercentageBase(reference);
                if (candidate == null)
                    throw new ArgumentException("This value does not have a percentage base.");
                if (basis != null && !FilterExpressionComparer.Instance.Equals(basis, candidate))
                    throw new ArgumentException("Cannot combine different percentage bases.");
                basis = candidate;
            }
            if (basis == null) throw new ArgumentException("A percentage comparison requires a reference value.");

            var replacement = new Replacement(basis.Type == typeof(double) ? basis : Expression.Convert(basis, typeof(double)));
            left = replacement.Visit(left);
            right = replacement.Visit(right);
        }

        private sealed class Context : ExpressionVisitor
        {
            internal bool HasPercentage;
            internal readonly List<MethodCallExpression> References = new();

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (node == percentageBase) HasPercentage = true;
                return node;
            }

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (node.Method.DeclaringType != typeof(StoredItemValueResolver)) return base.VisitMethodCall(node);
                References.Add(node);
                return node;
            }
        }

        private sealed class Replacement : ExpressionVisitor
        {
            private readonly Expression value;

            internal Replacement(Expression value) => this.value = value;

            protected override Expression VisitParameter(ParameterExpression node) =>
                node == percentageBase ? value : node;
        }
    }
}
