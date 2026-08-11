using System;
using System.Collections.Generic;

namespace JAEE.AX.EditorExtensions.Format
{
    /// <summary>
    /// X++ lexer. Consumes strings, comments and preprocessor directives whole so that
    /// braces/semicolons inside them can never affect layout. Notes for X++:
    ///  - only double quotes delimit strings; backslash is a literal path char, NOT an
    ///    escape (so "c:\temp\" tokenizes cleanly). A string that legitimately spans a
    ///    line, or an unclosed one, raises and the format becomes a safe no-op.
    ///  - a '#' at line start reads the whole line as a Directive (#define/#localmacro/…).
    ///    A '#name' elsewhere is an inline macro reference and stays one Word token.
    /// </summary>
    internal static class XppLexer
    {
        // Longest-match first where a shorter operator is a prefix of a longer one.
        private static readonly string[] Operators =
        {
            "===", "!==", ">>>", "<<=", ">>=", "??=",
            "=>", "?.", "**", "==", "!=", "<=", ">=", "&&", "||", "++", "--",
            "+=", "-=", "*=", "/=", "%=", "::", "->", "..", "<<", ">>", "??", "?:"
        };

        private const string SinglePunctuation = "(){}[],;:.";

        public static List<Token> Tokenize(string source)
        {
            var tokens = new List<Token>();
            int i = 0, n = source.Length;

            while (i < n)
            {
                char c = source[i];

                if (char.IsWhiteSpace(c))
                {
                    int start = i;
                    while (i < n && char.IsWhiteSpace(source[i])) i++;
                    tokens.Add(new Token(TokenType.Space, source.Substring(start, i - start)) { SourceStart = start });
                    continue;
                }

                // #directive on its own line
                if (c == '#' && AtLineStart(source, i))
                {
                    int start = i;
                    while (i < n && source[i] != '\n') i++;
                    string val = source.Substring(start, i - start).TrimEnd('\r', ' ', '\t');
                    tokens.Add(new Token(TokenType.Directive, val) { SourceStart = start });
                    continue;
                }

                // inline macro reference #name
                if (c == '#' && i + 1 < n && IsWordStart(source[i + 1]))
                {
                    int start = i;
                    i++;
                    while (i < n && IsWord(source[i])) i++;
                    tokens.Add(new Token(TokenType.Word, source.Substring(start, i - start)) { SourceStart = start });
                    continue;
                }

                // block comment
                if (c == '/' && i + 1 < n && source[i + 1] == '*')
                {
                    int blockStart = i;
                    int end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    if (end < 0) throw new XppFormatException("Unclosed block comment.");
                    int stop = end + 2;
                    tokens.Add(new Token(TokenType.Comment, source.Substring(i, stop - i)) { SourceStart = blockStart });
                    i = stop;
                    continue;
                }

                // line comment
                if (c == '/' && i + 1 < n && source[i + 1] == '/')
                {
                    int start = i;
                    while (i < n && source[i] != '\n') i++;
                    string val = source.Substring(start, i - start).TrimEnd('\r');
                    tokens.Add(new Token(TokenType.Comment, val) { SourceStart = start });
                    continue;
                }

                // string literal — X++ accepts BOTH double and single quotes; no backslash
                // escaping (backslash is a literal path char). Consumed whole so nothing
                // inside is ever reformatted.
                if (c == '"' || c == '\'')
                {
                    char quote = c;
                    int start = i;
                    i++;
                    bool closed = false;
                    while (i < n)
                    {
                        char s = source[i];
                        if (s == quote) { i++; closed = true; break; }
                        if (s == '\n') break;
                        i++;
                    }
                    if (!closed) throw new XppFormatException("Unclosed string literal.");
                    tokens.Add(new Token(TokenType.String, source.Substring(start, i - start)) { SourceStart = start });
                    continue;
                }

                // number
                if (IsDigit(c))
                {
                    int start = i;
                    i++;
                    while (i < n && (IsWord(source[i]) || source[i] == '.')) i++;
                    tokens.Add(new Token(TokenType.Number, source.Substring(start, i - start)) { SourceStart = start });
                    continue;
                }

                // word / identifier / keyword
                if (IsWordStart(c))
                {
                    int start = i;
                    i++;
                    while (i < n && IsWord(source[i])) i++;
                    tokens.Add(new Token(TokenType.Word, source.Substring(start, i - start)) { SourceStart = start });
                    continue;
                }

                // multi-char operator
                bool matched = false;
                int opStart = i;
                foreach (var op in Operators)
                {
                    if (i + op.Length <= n && string.CompareOrdinal(source, i, op, 0, op.Length) == 0)
                    {
                        tokens.Add(new Token(TokenType.Operator, op) { SourceStart = opStart });
                        i += op.Length;
                        matched = true;
                        break;
                    }
                }
                if (matched) continue;

                // single character
                int singleStart = i;
                char ch = source[i];
                i++;
                var singleTok = SinglePunctuation.IndexOf(ch) >= 0
                    ? new Token(TokenType.Punctuation, ch.ToString())
                    : new Token(TokenType.Operator, ch.ToString());
                singleTok.SourceStart = singleStart;
                tokens.Add(singleTok);
            }

            return tokens;
        }

        private static bool AtLineStart(string s, int i)
        {
            int j = i - 1;
            while (j >= 0 && (s[j] == ' ' || s[j] == '\t')) j--;
            return j < 0 || s[j] == '\n';
        }

        private static bool IsWordStart(char c) => char.IsLetter(c) || c == '_';
        private static bool IsWord(char c) => char.IsLetterOrDigit(c) || c == '_';
        private static bool IsDigit(char c) => c >= '0' && c <= '9';
    }
}
