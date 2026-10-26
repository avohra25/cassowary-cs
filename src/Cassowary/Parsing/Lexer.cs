using System.Collections.Generic;
using System.Globalization;

namespace Cassowary.Parsing;

internal enum TokenKind
{
    Number,
    Identifier,
    Plus,
    Minus,
    Star,
    Slash,
    LParen,
    RParen,
    Equal,
    LessEqual,
    GreaterEqual,
    At,
    Separator, // newline or ';'
    End,
}

internal readonly record struct Token(TokenKind Kind, string Text, double Number, int Line, int Column)
{
    public string Describe() => Kind switch
    {
        TokenKind.End => "end of input",
        TokenKind.Separator => Text == ";" ? "';'" : "end of line",
        _ => $"'{Text}'",
    };
}

internal static class Lexer
{
    public static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        int i = 0, line = 1, lineStart = 0;
        while (i < text.Length)
        {
            char c = text[i];
            int col = i - lineStart + 1;
            if (c == '\n')
            {
                tokens.Add(new Token(TokenKind.Separator, "\n", 0, line, col));
                i++;
                line++;
                lineStart = i;
                continue;
            }
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == '#')
            {
                while (i < text.Length && text[i] != '\n') i++;
                continue;
            }
            if (char.IsDigit(c) || (c == '.' && i + 1 < text.Length && char.IsDigit(text[i + 1])))
            {
                int start = i;
                while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.')) i++;
                if (i < text.Length && (text[i] == 'e' || text[i] == 'E'))
                {
                    int save = i++;
                    if (i < text.Length && (text[i] == '+' || text[i] == '-')) i++;
                    if (i < text.Length && char.IsDigit(text[i]))
                        while (i < text.Length && char.IsDigit(text[i])) i++;
                    else
                        i = save; // not an exponent after all ("2e" is 2 then identifier e)
                }
                string s = text[start..i];
                if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    throw new ParseException($"malformed number '{s}'", line, col);
                tokens.Add(new Token(TokenKind.Number, s, value, line, col));
                continue;
            }
            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_' || text[i] == '.')) i++;
                string s = text[start..i];
                if (s.EndsWith('.'))
                    throw new ParseException($"identifier '{s}' may not end with '.'", line, col);
                tokens.Add(new Token(TokenKind.Identifier, s, 0, line, col));
                continue;
            }

            string two = i + 1 < text.Length ? text.Substring(i, 2) : "";
            (TokenKind kind, int len) = two switch
            {
                "==" => (TokenKind.Equal, 2),
                "<=" => (TokenKind.LessEqual, 2),
                ">=" => (TokenKind.GreaterEqual, 2),
                _ => c switch
                {
                    '+' => (TokenKind.Plus, 1),
                    '-' => (TokenKind.Minus, 1),
                    '*' => (TokenKind.Star, 1),
                    '/' => (TokenKind.Slash, 1),
                    '(' => (TokenKind.LParen, 1),
                    ')' => (TokenKind.RParen, 1),
                    '=' => (TokenKind.Equal, 1),
                    '@' => (TokenKind.At, 1),
                    ';' => (TokenKind.Separator, 1),
                    '<' or '>' => throw new ParseException(
                        $"strict comparison '{c}' is not supported; use '{c}='", line, col),
                    _ => throw new ParseException($"unexpected character '{c}'", line, col),
                },
            };
            tokens.Add(new Token(kind, text.Substring(i, len), 0, line, col));
            i += len;
        }
        int endCol = text.Length - lineStart + 1;
        tokens.Add(new Token(TokenKind.End, "", 0, line, endCol));
        return tokens;
    }
}
