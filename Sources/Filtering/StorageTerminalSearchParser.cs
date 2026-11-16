using System;
using System.Collections.Generic;
using System.Text;

namespace DigitalStorage.Filtering
{
    internal static class StorageTerminalSearchParser
    {
        internal static StoredItemExpression Parse(string input)
        {
            var expression = StoredItemExpression.Constant(true);
            foreach (var token in Tokenize(input))
                expression = expression.And(ParseToken(token.Prefix, token.Text, token.Separator));
            return expression;
        }

        private static StoredItemExpression ParseToken(char prefix, string text, int separator)
        {
            if (prefix == '#')
            {
                var types = StoredItemExpression.Constant(false);
                for (int i = 0; i < TypeHelper.ALL_TYPES_LIST.Count; i++)
                {
                    string tag = TypeHelper.ALL_TYPES_LIST[i];
                    if (tag.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0
                        || TypeHelper.GetTypeDisplayName(tag).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                        types = types.Or(StoredItemExpression.ItemType(i));
                }
                return types;
            }

            if (prefix == '@')
            {
                if (separator < 0)
                    return StoredItemExpression.ModId.Contains(text, StringComparison.OrdinalIgnoreCase);
                return StoredItemExpression.ModId.Contains(text.Substring(0, separator), StringComparison.OrdinalIgnoreCase)
                    .And(StoredItemExpression.Identifier.Contains(text.Substring(separator + 1), StringComparison.OrdinalIgnoreCase));
            }

            return StoredItemExpression.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<(char Prefix, string Text, int Separator)> Tokenize(string input)
        {
            var text = new StringBuilder();
            char prefix = '\0';
            char quote = '\0';
            int separator = -1;
            bool started = false;

            for (int i = 0; i < input.Length; i++)
            {
                char ch = input[i];
                if (ch == '\\')
                {
                    text.Append(i + 1 < input.Length ? input[++i] : ch);
                }
                else if (quote != '\0')
                {
                    if (ch == quote) quote = '\0';
                    else text.Append(ch);
                }
                else if (ch == '"' || ch == '\'') quote = ch;
                else if (char.IsWhiteSpace(ch))
                {
                    if (started) yield return (prefix, text.ToString(), separator);
                    text.Clear();
                    prefix = '\0';
                    separator = -1;
                    started = false;
                    continue;
                }
                else if (!started && (ch == '#' || ch == '@')) prefix = ch;
                else
                {
                    if (prefix == '@' && ch == ':' && separator < 0) separator = text.Length;
                    text.Append(ch);
                }
                started = true;
            }

            if (started) yield return (prefix, text.ToString(), separator);
        }
    }
}
