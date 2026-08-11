namespace JAEE.AX.EditorExtensions.Format
{
    internal enum TokenType
    {
        Space,
        Comment,    // // line comment or /* block comment */
        String,     // "double-quoted"
        Number,
        Word,       // identifier / keyword / #macroReference
        Operator,
        Punctuation, // one of ( ) { } [ ] , ; : .
        Directive    // whole-line X++ preprocessor line: #define / #localmacro / ...
    }

    internal sealed class Token
    {
        public TokenType Type;
        public string Value;

        /// <summary>Number of newlines in the whitespace immediately preceding this
        /// (significant) token. 0 means it sat on the same source line as the token
        /// before it — used to keep trailing comments inline and to preserve blank lines.</summary>
        public int NewlinesBefore;

        /// <summary>Set on a top-level ',' whose parenthesized argument list spans multiple
        /// source lines — the renderer breaks the line after it ("chop when multiline").</summary>
        public bool BreakAfter;

        /// <summary>Set on a '(' whose argument list spans multiple source lines.</summary>
        public bool GroupMultiline;

        /// <summary>Set on a ternary '?' / ':' that should start its own (indented) line,
        /// because the ternary was written across multiple source lines.</summary>
        public bool BreakBefore;

        /// <summary>Character offset of this token's first character in the source string.</summary>
        public int SourceStart;

        public Token(TokenType type, string value)
        {
            Type = type;
            Value = value;
        }

        public bool Is(string v) => Value == v;
    }
}
