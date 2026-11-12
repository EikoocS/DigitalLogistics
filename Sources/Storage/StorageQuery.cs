using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using System;
using System.Globalization;

namespace DigitalStorage.Storage
{
    public class StorageQuery
    {
        public static readonly Comparer<StoredItem> DisplayNameComparer = Comparer<StoredItem>.Create((a, b) => a.DisplayName.CompareTo(b.DisplayName));
        public static readonly Comparer<StoredItem> PriceComparer = Comparer<StoredItem>.Create((a, b) =>
        {
            int result = a.Price.CompareTo(b.Price);
            return result != 0 ? result : DisplayNameComparer.Compare(a, b);
        });
        public static readonly Comparer<StoredItem> HeatComparer = Comparer<StoredItem>.Create((a, b) =>
        {
            int result = Nullable.Compare(a.Heat, b.Heat);
            return result != 0 ? result : DisplayNameComparer.Compare(a, b);
        });
        private StringComparison nameComparison = StringComparison.Ordinal;
        private Regex namePattern = null;
        public readonly HashSet<string> requiredNames = new();
        public readonly HashSet<string> anyNames = new();
        public readonly HashSet<string> excludedNames = new();
        public readonly List<(PriceComparison Comparison, decimal Value)> priceFilters = new();
        public SortField sort = SortField.DISPLAY_NAME;
        public bool descending = false;
        public ulong requiredMask = 0;
        public ulong anyMask = 0;
        public ulong excludedMask = 0;

        public StorageQuery()
        {
        }

        public StorageQuery(string input, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            nameComparison = comparison;
            foreach (var token in Tokenize(input)) ParseToken(token.Text, token.PrefixLength);
        }

        public void setNameMatcher(string matcher, StringComparison comparison = StringComparison.Ordinal)
        {
            requiredNames.Clear();
            anyNames.Clear();
            excludedNames.Clear();
            if (matcher != null) requiredNames.Add(matcher);
            nameComparison = comparison;
        }

        public void setNamePattern(string pattern)
        {
            this.namePattern = new Regex(pattern);
        }

        public IComparer<StoredItem> GetComparer() => sort switch
        {
            SortField.PRICE => PriceComparer,
            SortField.STOLEN => HeatComparer,
            _ => DisplayNameComparer
        };

        public bool matchPrice(long price)
        {
            foreach (var filter in priceFilters)
            {
                bool matches = filter.Comparison switch
                {
                    PriceComparison.LESS => price < filter.Value,
                    PriceComparison.LESS_OR_EQUAL => price <= filter.Value,
                    PriceComparison.GREATER => price > filter.Value,
                    PriceComparison.GREATER_OR_EQUAL => price >= filter.Value,
                    _ => price == filter.Value
                };
                if (!matches) return false;
            }
            return true;
        }

        public bool matchName(string name)
        {
            if (requiredNames.Count == 0 && anyNames.Count == 0 && excludedNames.Count == 0)
                return namePattern == null || namePattern.IsMatch(name);

            foreach (var keyword in requiredNames)
                if (name.IndexOf(keyword, nameComparison) < 0) return false;

            foreach (var keyword in excludedNames)
                if (name.IndexOf(keyword, nameComparison) >= 0) return false;

            if (anyNames.Count == 0) return true;
            foreach (var keyword in anyNames)
                if (name.IndexOf(keyword, nameComparison) >= 0) return true;
            return false;
        }

        private void ParseToken(string text, int prefixLength)
        {
            if (prefixLength == 0)
            {
                requiredNames.Add(text);
                return;
            }

            switch (text[0])
            {
                case '@':
                    if (text.Equals("@DEC", StringComparison.OrdinalIgnoreCase)) descending = true;
                    else if (text.Equals("@SAFE", StringComparison.OrdinalIgnoreCase))
                        excludedMask |= StorageItemTypeHelper.GetUnsafeMask();
                    else if (text.Equals("@UNSAFE", StringComparison.OrdinalIgnoreCase))
                        anyMask |= StorageItemTypeHelper.GetUnsafeMask();
                    else if (text.Equals("@PRICE", StringComparison.OrdinalIgnoreCase)) sort = SortField.PRICE;
                    else if (text.Equals("@STOLEN", StringComparison.OrdinalIgnoreCase)) sort = SortField.STOLEN;
                    break;
                case '<':
                case '>':
                case '=':
                    int valueOffset = prefixLength > 1 && text[0] != '=' && text[1] == '=' ? 2 : 1;
                    if (decimal.TryParse(text.Substring(valueOffset),
                        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture, out decimal price))
                    {
                        var comparison = text[0] switch
                        {
                            '<' => valueOffset == 2 ? PriceComparison.LESS_OR_EQUAL : PriceComparison.LESS,
                            '>' => valueOffset == 2 ? PriceComparison.GREATER_OR_EQUAL : PriceComparison.GREATER,
                            _ => PriceComparison.EQUAL
                        };
                        priceFilters.Add((comparison, price));
                    }
                    break;
                case '#':
                    int offset = 1;
                    char mode = '\0';
                    if (prefixLength > 1 && (text[1] == '!' || text[1] == '?'))
                    {
                        mode = text[1];
                        offset = 2;
                    }
                    if (text.Length == offset) break;
                    ulong mask = StorageItemTypeHelper.FromLocalizedName(text.Substring(offset));
                    if (mode == '!') excludedMask |= mask;
                    else if (mode == '?') anyMask |= mask;
                    else requiredMask |= mask;
                    break;
                case '!':
                    if (text.Length > 1) excludedNames.Add(text.Substring(1));
                    break;
                case '?':
                    if (text.Length > 1) anyNames.Add(text.Substring(1));
                    break;
                default:
                    requiredNames.Add(text);
                    break;
            }
        }

        private static IEnumerable<(string Text, int PrefixLength)> Tokenize(string input)
        {
            var text = new StringBuilder();
            char quote = '\0';
            int prefixLength = 0;
            bool prefixEnded = false;

            for (int i = 0; i < input.Length; i++)
            {
                char character = input[i];
                if (character == '\\')
                {
                    text.Append(i + 1 < input.Length ? input[++i] : character);
                    prefixEnded = true;
                }
                else if (quote != '\0')
                {
                    if (character == quote) quote = '\0';
                    else text.Append(character);
                }
                else if (character == '\'' || character == '"')
                {
                    quote = character;
                    prefixEnded = true;
                }
                else if (char.IsWhiteSpace(character))
                {
                    if (text.Length > 0) yield return (text.ToString(), prefixLength);
                    text.Clear();
                    prefixLength = 0;
                    prefixEnded = false;
                }
                else
                {
                    text.Append(character);
                    if (!prefixEnded) prefixLength++;
                }
            }

            if (text.Length > 0) yield return (text.ToString(), prefixLength);
        }

        public enum SortField 
        {
            DISPLAY_NAME,
            PRICE,
            STOLEN
        }

        public enum PriceComparison
        {
            LESS,
            LESS_OR_EQUAL,
            EQUAL,
            GREATER_OR_EQUAL,
            GREATER
        }
    }
}
