using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HunterWidow.Domain.Content
{
    public sealed class JsonParseException : Exception
    {
        public JsonParseException(string message, int line, int column)
            : base(message)
        {
            Line = line;
            Column = column;
        }

        public int Line { get; }

        public int Column { get; }
    }

    /// <summary>
    /// A deliberately small JSON reader shared by Unity and the .NET content tools.
    /// It returns dictionaries, lists, strings, doubles, booleans, or null.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string text)
        {
            var parser = new Parser(text ?? string.Empty);
            var value = parser.ParseValue();
            parser.SkipWhitespace();

            if (!parser.IsAtEnd)
            {
                parser.Throw("Unexpected text after the JSON value.");
            }

            return value;
        }

        private sealed class Parser
        {
            private readonly string text;
            private int index;
            private int line = 1;
            private int column = 1;

            public Parser(string text)
            {
                this.text = text;
            }

            public bool IsAtEnd => index >= text.Length;

            private char Current => IsAtEnd ? '\0' : text[index];

            public object ParseValue()
            {
                SkipWhitespace();

                if (IsAtEnd)
                {
                    Throw("Expected a JSON value.");
                }

                switch (Current)
                {
                    case '{':
                        return ParseObject();
                    case '[':
                        return ParseArray();
                    case '"':
                        return ParseString();
                    case 't':
                        return ParseLiteral("true", true);
                    case 'f':
                        return ParseLiteral("false", false);
                    case 'n':
                        return ParseLiteral("null", null);
                    default:
                        if (Current == '-' || char.IsDigit(Current))
                        {
                            return ParseNumber();
                        }

                        Throw("Expected a JSON value.");
                        return null;
                }
            }

            public void SkipWhitespace()
            {
                while (!IsAtEnd && char.IsWhiteSpace(Current))
                {
                    Advance();
                }
            }

            public void Throw(string message)
            {
                throw new JsonParseException(message, line, column);
            }

            private Dictionary<string, object> ParseObject()
            {
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                Expect('{');
                SkipWhitespace();

                if (TryConsume('}'))
                {
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();
                    if (Current != '"')
                    {
                        Throw("Expected an object property name.");
                    }

                    var key = ParseString();
                    SkipWhitespace();
                    Expect(':');
                    var value = ParseValue();
                    result[key] = value;
                    SkipWhitespace();

                    if (TryConsume('}'))
                    {
                        return result;
                    }

                    Expect(',');
                }
            }

            private List<object> ParseArray()
            {
                var result = new List<object>();
                Expect('[');
                SkipWhitespace();

                if (TryConsume(']'))
                {
                    return result;
                }

                while (true)
                {
                    result.Add(ParseValue());
                    SkipWhitespace();

                    if (TryConsume(']'))
                    {
                        return result;
                    }

                    Expect(',');
                }
            }

            private string ParseString()
            {
                var builder = new StringBuilder();
                Expect('"');

                while (!IsAtEnd)
                {
                    var character = Current;
                    Advance();

                    if (character == '"')
                    {
                        return builder.ToString();
                    }

                    if (character < ' ')
                    {
                        Throw("Control characters must be escaped in strings.");
                    }

                    if (character != '\\')
                    {
                        builder.Append(character);
                        continue;
                    }

                    if (IsAtEnd)
                    {
                        Throw("Unterminated string escape.");
                    }

                    var escape = Current;
                    Advance();
                    switch (escape)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u': builder.Append(ParseUnicodeEscape()); break;
                        default: Throw("Invalid string escape."); break;
                    }
                }

                Throw("Unterminated string.");
                return string.Empty;
            }

            private char ParseUnicodeEscape()
            {
                if (index + 4 > text.Length)
                {
                    Throw("Incomplete Unicode escape.");
                }

                var digits = text.Substring(index, 4);
                for (var digitIndex = 0; digitIndex < 4; digitIndex++)
                {
                    Advance();
                }

                ushort value;
                if (!ushort.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                {
                    Throw("Invalid Unicode escape.");
                }

                return (char)value;
            }

            private object ParseLiteral(string literal, object value)
            {
                for (var characterIndex = 0; characterIndex < literal.Length; characterIndex++)
                {
                    if (IsAtEnd || Current != literal[characterIndex])
                    {
                        Throw("Invalid JSON literal.");
                    }

                    Advance();
                }

                return value;
            }

            private double ParseNumber()
            {
                var start = index;

                if (Current == '-')
                {
                    Advance();
                }

                ConsumeDigits();

                if (Current == '.')
                {
                    Advance();
                    ConsumeDigits();
                }

                if (Current == 'e' || Current == 'E')
                {
                    Advance();
                    if (Current == '+' || Current == '-')
                    {
                        Advance();
                    }

                    ConsumeDigits();
                }

                var value = text.Substring(start, index - start);
                double parsed;
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                {
                    Throw("Invalid JSON number.");
                }

                return parsed;
            }

            private void ConsumeDigits()
            {
                var start = index;
                while (!IsAtEnd && char.IsDigit(Current))
                {
                    Advance();
                }

                if (start == index)
                {
                    Throw("Expected a digit.");
                }
            }

            private void Expect(char expected)
            {
                SkipWhitespace();
                if (Current != expected)
                {
                    Throw("Expected '" + expected + "'.");
                }

                Advance();
            }

            private bool TryConsume(char expected)
            {
                if (Current != expected)
                {
                    return false;
                }

                Advance();
                return true;
            }

            private void Advance()
            {
                if (IsAtEnd)
                {
                    return;
                }

                var consumed = text[index++];
                if (consumed == '\r')
                {
                    if (!IsAtEnd && Current == '\n')
                    {
                        index++;
                    }

                    line++;
                    column = 1;
                    return;
                }

                if (consumed == '\n')
                {
                    line++;
                    column = 1;
                    return;
                }

                column++;
            }
        }
    }
}
