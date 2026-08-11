using System;

namespace JAEE.AX.EditorExtensions.Format
{
    /// <summary>
    /// Raised by the lexer/formatter when the input cannot be tokenized or balanced
    /// (unclosed string/comment, unbalanced brackets). The public entry point catches
    /// it and returns the original text unchanged — a format is never partial.
    /// </summary>
    internal sealed class XppFormatException : Exception
    {
        public XppFormatException(string message) : base(message) { }
    }
}
