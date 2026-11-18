using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using DigitalLogistics.Storage.Items;
using Superpower;
using Superpower.Model;
using Superpower.Parsers;

namespace DigitalLogistics.Filtering
{
    public static class StoredItemExpressionParser
    {
        private static readonly Dictionary<string, string> PropertyGetters = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Identifier"] = nameof(StoredItemValueResolver.ReadIdentifier),
            ["ident"] = nameof(StoredItemValueResolver.ReadIdentifier),
            ["ModId"] = nameof(StoredItemValueResolver.ReadModId),
            ["mod"] = nameof(StoredItemValueResolver.ReadModId),
            ["DisplayName"] = nameof(StoredItemValueResolver.ReadDisplayName),
            ["name"] = nameof(StoredItemValueResolver.ReadDisplayName),
            ["Price"] = nameof(StoredItemValueResolver.ReadPrice),
            ["BasePrice"] = nameof(StoredItemValueResolver.ReadBasePrice),
            ["Heat"] = nameof(StoredItemValueResolver.ReadHeat),
            ["ContrabandLevel"] = nameof(StoredItemValueResolver.ReadContrabandLevel),
            ["level"] = nameof(StoredItemValueResolver.ReadContrabandLevel)
        };

        private static readonly StoredItemExpressionTokenizer Tokenizer = new();
        private static readonly TokenListParser<FilterToken, Expression> Parser = BuildParser();

        public static StoredItemExpression Parse(string input)
        {
            var tokens = Tokenizer.Tokenize(input);
            var root = Parser.Parse(tokens);
            if (root.Type != typeof(bool))
                throw new ParseException("A filter expression must return bool.", new TextSpan(input).Position);
            return new StoredItemExpression(root);
        }

        private static TokenListParser<FilterToken, Expression> BuildParser()
        {
            TokenListParser<FilterToken, Expression> expression = null;
            TokenListParser<FilterToken, Expression> unary = null;

            var value = Operator(FilterToken.Word, FilterToken.String, FilterToken.Property,
                    FilterToken.TagValue, FilterToken.ItemType, FilterToken.Percentage, FilterToken.Alias)
                .Select(ParseValue);

            var parentheses =
                from open in Token.EqualTo(FilterToken.LeftParenthesis)
                from inner in Superpower.Parse.Ref(() => expression)
                from close in Token.EqualTo(FilterToken.RightParenthesis)
                select inner;

            var prefix =
                from op in Operator(FilterToken.Plus, FilterToken.Minus, FilterToken.Not)
                from operand in Superpower.Parse.Ref(() => unary)
                select AtToken(op, () => FilterExpressionBuilder.Unary(op.Kind switch
                {
                    FilterToken.Not => ExpressionType.Not,
                    FilterToken.Minus => ExpressionType.Negate,
                    _ => ExpressionType.UnaryPlus
                }, operand));

            unary = prefix.Or(parentheses).Or(value).Named("operand");
            var product = Chain(unary, FilterToken.Multiply, FilterToken.Divide);
            var sum = Chain(product, FilterToken.Plus, FilterToken.Minus);
            var comparison = Chain(sum, FilterToken.Greater, FilterToken.Less,
                FilterToken.GreaterOrEqual, FilterToken.LessOrEqual, FilterToken.In, FilterToken.Match);
            var equality = Chain(comparison, FilterToken.Equal);
            var and = Chain(equality, FilterToken.And);
            var xor = Chain(and, FilterToken.Xor);
            expression = Chain(xor, FilterToken.Or);
            return expression.AtEnd();
        }

        private static Expression ParseValue(Token<FilterToken> token)
        {
            string text = token.ToStringValue();
            switch (token.Kind)
            {
                case FilterToken.Percentage:
                    double percentage = double.Parse(text.Substring(0, text.Length - 1),
                        NumberStyles.Float, CultureInfo.InvariantCulture);
                    return FilterPercentages.Create(percentage);

                case FilterToken.Alias:
                    return AtToken(token, () => FilterAliases.Resolve(Decode(text.Substring(1, text.Length - 2))));

                case FilterToken.Property:
                    string name = Decode(text.Substring(1));
                    if (!PropertyGetters.TryGetValue(name, out string propertyGetter))
                        throw Error(token, "Unknown property '@" + name + "'.");
                    return FilterExpressionBuilder.Read(propertyGetter);

                case FilterToken.ItemType:
                    string type = DecodeString(text.Substring(1));
                    int index = ItemTypeMask.MatchIndexFromString(type);
                    if (index < 0) throw Error(token, "Unknown item type '#" + type + "'.");
                    return FilterExpressionBuilder.Read(nameof(StoredItemValueResolver.ReadItemType),
                        Expression.Constant(index));

                case FilterToken.TagValue:
                    int separator = text.LastIndexOf("].", StringComparison.Ordinal);
                    string key = Decode(text.Substring(1, separator - 1));
                    string getter = text.Substring(separator + 2).ToLowerInvariant() switch
                    {
                        "int" => nameof(StoredItemValueResolver.ReadInt),
                        "float" => nameof(StoredItemValueResolver.ReadFloat),
                        "long" => nameof(StoredItemValueResolver.ReadLong),
                        "double" => nameof(StoredItemValueResolver.ReadDouble),
                        "bool" => nameof(StoredItemValueResolver.ReadBool),
                        "string" => nameof(StoredItemValueResolver.ReadString),
                        _ => throw Error(token, "Unknown value type in '" + text + "'.")
                    };
                    return FilterExpressionBuilder.Read(getter, Expression.Constant(key));

                case FilterToken.String:
                    return Expression.Constant(DecodeString(text));

                default:
                    if (text.IndexOf('\\') >= 0) return Expression.Constant(Decode(text));
                    if (bool.TryParse(text, out bool boolean)) return Expression.Constant(boolean);
                    if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int integer))
                        return Expression.Constant(integer);
                    if (long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out long large))
                        return Expression.Constant(large);
                    bool digitsOnly = text.Length > 0;
                    foreach (char ch in text) digitsOnly &= ch >= '0' && ch <= '9';
                    if (digitsOnly) throw Error(token, "Integer literal is outside the range of long.");
                    if ((char.IsDigit(text[0]) || text[0] == '.')
                        && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                        return Expression.Constant(number);
                    return Expression.Constant(text);
            }
        }

        private static string DecodeString(string text) =>
            text[0] == '"' || text[0] == '\'' || text[0] == '“'
                ? Decode(text.Substring(1, text.Length - 2))
                : Decode(text);

        private static string Decode(string text)
        {
            if (text.IndexOf('\\') < 0) return text;
            var decoded = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
                decoded.Append(text[i] == '\\' ? text[++i] : text[i]);
            return decoded.ToString();
        }

        private static ParseException Error(Token<FilterToken> token, string message) =>
            new ParseException(message + " (" + token.Position + ")", token.Position);

        private static TokenListParser<FilterToken, Expression> Chain(
            TokenListParser<FilterToken, Expression> operand, params FilterToken[] operators) =>
            Superpower.Parse.Chain(Operator(operators), operand, Binary);

        private static Expression Binary(Token<FilterToken> op, Expression left, Expression right) =>
            AtToken(op, () => op.Kind switch
            {
                FilterToken.In => FilterExpressionBuilder.Contains(right, left),
                FilterToken.Match => FilterExpressionBuilder.Matches(right, left),
                _ => FilterExpressionBuilder.Binary(op.Kind switch
                {
                    FilterToken.And => ExpressionType.AndAlso,
                    FilterToken.Or => ExpressionType.OrElse,
                    FilterToken.Xor => ExpressionType.ExclusiveOr,
                    FilterToken.Equal => ExpressionType.Equal,
                    FilterToken.Plus => ExpressionType.Add,
                    FilterToken.Minus => ExpressionType.Subtract,
                    FilterToken.Multiply => ExpressionType.Multiply,
                    FilterToken.Divide => ExpressionType.Divide,
                    FilterToken.Greater => ExpressionType.GreaterThan,
                    FilterToken.Less => ExpressionType.LessThan,
                    FilterToken.GreaterOrEqual => ExpressionType.GreaterThanOrEqual,
                    FilterToken.LessOrEqual => ExpressionType.LessThanOrEqual,
                    _ => throw new InvalidOperationException()
                }, left, right)
            });

        private static Expression AtToken(Token<FilterToken> token, Func<Expression> build)
        {
            try
            {
                return build();
            }
            catch (ArgumentException error)
            {
                throw Error(token, error.Message);
            }
        }

        private static TokenListParser<FilterToken, Token<FilterToken>> Operator(params FilterToken[] kinds)
        {
            var parser = Token.EqualTo(kinds[0]);
            for (int i = 1; i < kinds.Length; i++) parser = parser.Or(Token.EqualTo(kinds[i]));
            return parser;
        }
    }
}
