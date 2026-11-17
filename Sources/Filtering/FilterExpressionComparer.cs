using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DigitalLogistics.Filtering
{
    internal sealed class FilterExpressionComparer : IEqualityComparer<Expression>
    {
        public static readonly FilterExpressionComparer Instance = new();

        public bool Equals(Expression x, Expression y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x == null || y == null || x.NodeType != y.NodeType || x.Type != y.Type) return false;
            switch (x)
            {
                case ConstantExpression constant:
                    return object.Equals(constant.Value, ((ConstantExpression)y).Value);
                case MethodCallExpression call:
                    var otherCall = (MethodCallExpression)y;
                    if (call.Method != otherCall.Method || !Equals(call.Object, otherCall.Object)
                        || call.Arguments.Count != otherCall.Arguments.Count) return false;
                    for (int i = 0; i < call.Arguments.Count; i++)
                        if (!Equals(call.Arguments[i], otherCall.Arguments[i])) return false;
                    return true;
                case UnaryExpression unary:
                    var otherUnary = (UnaryExpression)y;
                    return unary.Method == otherUnary.Method && Equals(unary.Operand, otherUnary.Operand);
                case BinaryExpression binary:
                    var otherBinary = (BinaryExpression)y;
                    return binary.Method == otherBinary.Method
                        && binary.IsLiftedToNull == otherBinary.IsLiftedToNull
                        && Equals(binary.Left, otherBinary.Left) && Equals(binary.Right, otherBinary.Right)
                        && Equals(binary.Conversion, otherBinary.Conversion);
                case MemberExpression member:
                    var otherMember = (MemberExpression)y;
                    return member.Member == otherMember.Member && Equals(member.Expression, otherMember.Expression);
                default:
                    return false;
            }
        }

        public int GetHashCode(Expression expression)
        {
            if (expression == null) return 0;
            var hash = new HashCode();
            hash.Add(expression.NodeType);
            hash.Add(expression.Type);
            switch (expression)
            {
                case ConstantExpression constant:
                    hash.Add(constant.Value);
                    break;
                case MethodCallExpression call:
                    hash.Add(call.Method);
                    hash.Add(GetHashCode(call.Object));
                    foreach (var argument in call.Arguments) hash.Add(GetHashCode(argument));
                    break;
                case UnaryExpression unary:
                    hash.Add(unary.Method);
                    hash.Add(GetHashCode(unary.Operand));
                    break;
                case BinaryExpression binary:
                    hash.Add(binary.Method);
                    hash.Add(binary.IsLiftedToNull);
                    hash.Add(GetHashCode(binary.Left));
                    hash.Add(GetHashCode(binary.Right));
                    hash.Add(GetHashCode(binary.Conversion));
                    break;
                case MemberExpression member:
                    hash.Add(member.Member);
                    hash.Add(GetHashCode(member.Expression));
                    break;
                default:
                    hash.Add(expression);
                    break;
            }
            return hash.ToHashCode();
        }
    }
}
