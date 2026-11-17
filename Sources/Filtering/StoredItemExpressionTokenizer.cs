using System;
using System.Collections.Generic;
using Superpower;
using Superpower.Model;

namespace DigitalLogistics.Filtering
{
    internal enum FilterToken
    {
        Word, String, Property, TagValue, ItemType,
        LeftParenthesis, RightParenthesis,
        Plus, Minus, Multiply, Divide,
        Greater, Less, Equal, GreaterOrEqual, LessOrEqual, In, Match,
        And, Or, Not, Xor
    }

    internal sealed class StoredItemExpressionTokenizer : Tokenizer<FilterToken>
    {
        protected override IEnumerable<Result<FilterToken>> Tokenize(TextSpan span)
        {
            while (!span.IsAtEnd)
            {
                string source = span.Source;
                int start = span.Position.Absolute;
                int end = start;
                if (char.IsWhiteSpace(source[start]))
                {
                    while (end < source.Length && char.IsWhiteSpace(source[end])) end++;
                    span = span.Skip(end - start);
                    continue;
                }

                FilterToken kind;
                char ch = source[end++];
                switch (ch)
                {
                    case '(': kind = FilterToken.LeftParenthesis; break;
                    case ')': kind = FilterToken.RightParenthesis; break;
                    case '+': kind = FilterToken.Plus; break;
                    case '-': kind = FilterToken.Minus; break;
                    case '*': kind = FilterToken.Multiply; break;
                    case '/': kind = FilterToken.Divide; break;
                    case '&': kind = FilterToken.And; break;
                    case '|': kind = FilterToken.Or; break;
                    case '!': kind = FilterToken.Not; break;
                    case '=':
                        if (end < source.Length && source[end] == '=') end++;
                        kind = FilterToken.Equal;
                        break;
                    case '>':
                    case '<':
                        bool equal = end < source.Length && source[end] == '=';
                        if (equal) end++;
                        kind = ch == '>'
                            ? (equal ? FilterToken.GreaterOrEqual : FilterToken.Greater)
                            : (equal ? FilterToken.LessOrEqual : FilterToken.Less);
                        break;
                    case '"':
                    case '\'':
                    case '“':
                        end = ReadQuoted(source, start, span);
                        kind = FilterToken.String;
                        break;
                    case '@':
                    case '#':
                        int nameStart = end;
                        if (ch == '#' && end < source.Length && IsQuote(source[end]))
                            end = ReadQuoted(source, end, span);
                        else
                            end = ReadWord(source, end, span);
                        if (end == nameStart) throw Error(span, "Expected a name after '" + ch + "'.");
                        kind = ch == '@' ? FilterToken.Property : FilterToken.ItemType;
                        break;
                    case '[':
                        while (end < source.Length && source[end] != ']')
                        {
                            if (source[end] == '\\') end = SkipEscape(source, end, span);
                            else end++;
                        }
                        if (end == source.Length) throw Error(span, "Expected ']'.");
                        end++;
                        if (end == source.Length || source[end++] != '.')
                            throw Error(span, "Expected a type suffix after '[key]'.");
                        int typeStart = end;
                        while (end < source.Length && char.IsLetter(source[end])) end++;
                        if (end == typeStart) throw Error(span, "Expected a type name after '.'.");
                        kind = FilterToken.TagValue;
                        break;
                    default:
                        end = ReadWord(source, start, span);
                        if (end == start) throw Error(span, "Unexpected character '" + ch + "'.");
                        kind = source.Substring(start, end - start).ToUpperInvariant() switch
                        {
                            "AND" => FilterToken.And,
                            "OR" => FilterToken.Or,
                            "NOT" => FilterToken.Not,
                            "XOR" => FilterToken.Xor,
                            "IN" => FilterToken.In,
                            "MATCH" => FilterToken.Match,
                            _ => FilterToken.Word
                        };
                        break;
                }

                var remainder = span.Skip(end - start);
                yield return Result.Value(kind, span, remainder);
                span = remainder;
            }
        }

        private static int ReadWord(string source, int offset, TextSpan span)
        {
            while (offset < source.Length)
            {
                char ch = source[offset];
                if (ch == '\\') offset = SkipEscape(source, offset, span);
                else if (char.IsWhiteSpace(ch) || "()<>!=&|+-*/@#[]\"'“”".IndexOf(ch) >= 0) break;
                else offset++;
            }
            return offset;
        }

        private static int ReadQuoted(string source, int offset, TextSpan span)
        {
            char close = source[offset] == '“' ? '”' : source[offset];
            offset++;
            while (offset < source.Length)
            {
                if (source[offset] == '\\') offset = SkipEscape(source, offset, span);
                else if (source[offset++] == close) return offset;
            }
            throw Error(span, "Expected closing quote '" + close + "'.");
        }

        private static int SkipEscape(string source, int offset, TextSpan span)
        {
            if (offset + 1 == source.Length) throw Error(span, "Expected a character after '\\'.");
            return offset + 2;
        }

        private static bool IsQuote(char ch) => ch == '"' || ch == '\'' || ch == '“';

        private static ParseException Error(TextSpan span, string message) =>
            new ParseException(message + " (" + span.Position + ")", span.Position);
    }
}
