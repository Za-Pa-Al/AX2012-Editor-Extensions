using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace JAEE.AX.EditorExtensions.Format
{
    /// <summary>
    /// Token-based X++ formatter. Pipeline:
    ///   tokenize -> canonicalize keyword casing -> balance check -> mark select spans
    ///   -> render (Phase A: braces/indent/spacing) -> align columns (Phase B)
    ///   -> verify token safety.
    /// The safety verifier re-tokenizes the output; if the significant token stream
    /// differs from the input (whitespace ignored, X++ words case-insensitive) the
    /// original text is returned unchanged. Every rule only moves whitespace, so the
    /// worst case is a no-op — never corrupted code.
    /// </summary>
    internal static class XppFormatter
    {
        private const int Tab = 4;

        // ---- public entry ---------------------------------------------------

        public static string Format(string source)
        {
            if (string.IsNullOrEmpty(source)) return source;
            try
            {
                var all = XppLexer.Tokenize(source);
                Canonicalize(all);
                var sig = Significant(all);
                if (sig.Count == 0) return source;
                BalancedTokens(sig);
                MarkCommaBreaks(sig);
                MarkTernaryBreaks(sig);

                string rendered = new Renderer(sig).Run();
                string aligned = AlignColumns(rendered);
                aligned = EnsureBlankAfterDeclarations(aligned);

                string body = aligned.TrimEnd('\n', '\r', ' ', '\t');
                char lastChar = source[source.Length - 1];
                bool srcEndsNewline = lastChar == '\n' || lastChar == '\r';
                string result = srcEndsNewline ? body + "\n" : body;

                if (!TokenStreamsEqual(source, result))
                    return source; // safety: refuse to change meaning

                // preserve the source's newline convention (the editor buffer is usually CRLF)
                if (source.IndexOf("\r\n", StringComparison.Ordinal) >= 0)
                    result = result.Replace("\n", "\r\n");

                return result;
            }
            catch (XppFormatException)
            {
                return source;
            }
        }

        // ---- keyword casing -------------------------------------------------

        private static readonly Dictionary<string, string> KeywordCase =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "ttsbegin", "ttsBegin" }, { "ttscommit", "ttsCommit" }, { "ttsabort", "ttsAbort" },
                { "firstonly", "firstOnly" }, { "firstonly10", "firstOnly10" },
                { "firstonly100", "firstOnly100" }, { "firstonly1000", "firstOnly1000" },
                { "firstfast", "firstFast" }, { "forupdate", "forUpdate" },
                { "crosscompany", "crossCompany" }, { "validtimestate", "validTimeState" },
                { "notexists", "notExists" }, { "optimisticlock", "optimisticLock" },
                { "pessimisticlock", "pessimisticLock" }, { "repeatableread", "repeatableRead" },
                { "generateonly", "generateOnly" }, { "forcenestedloop", "forceNestedLoop" },
                { "forceplaceholders", "forcePlaceholders" }
            };

        private static void Canonicalize(List<Token> tokens)
        {
            foreach (var t in tokens)
            {
                if (t.Type != TokenType.Word) continue;
                if (KeywordCase.TryGetValue(t.Value, out string canon))
                    t.Value = canon;
            }
        }

        // ---- significant token stream (annotated with NewlinesBefore) -------

        private static List<Token> Significant(List<Token> all)
        {
            var result = new List<Token>();
            int pending = 0;
            bool sawAny = false;
            foreach (var t in all)
            {
                if (t.Type == TokenType.Space)
                {
                    pending += CountNewlines(t.Value);
                    continue;
                }
                t.NewlinesBefore = sawAny ? pending : 0;
                result.Add(t);
                pending = 0;
                sawAny = true;
            }
            return result;
        }

        private static int CountNewlines(string s)
        {
            int c = 0;
            foreach (char ch in s) if (ch == '\n') c++;
            return c;
        }

        // ---- bracket balance ------------------------------------------------

        private static void BalancedTokens(List<Token> tokens)
        {
            var stack = new Stack<string>();
            foreach (var t in tokens)
            {
                if (t.Type != TokenType.Punctuation) continue;
                switch (t.Value)
                {
                    case "(": case "[": case "{":
                        stack.Push(t.Value);
                        break;
                    case ")": Expect(stack, "("); break;
                    case "]": Expect(stack, "["); break;
                    case "}": Expect(stack, "{"); break;
                }
            }
            if (stack.Count > 0)
                throw new XppFormatException("Unbalanced brackets.");
        }

        private static void Expect(Stack<string> stack, string open)
        {
            if (stack.Count == 0 || stack.Pop() != open)
                throw new XppFormatException("Unbalanced brackets.");
        }

        // ---- select statement formatting (data used by the renderer) -------

        private static readonly HashSet<string> SelectModifiers =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "firstonly", "firstonly10", "firstonly100", "firstonly1000", "firstfast",
                "forupdate", "nofetch", "crosscompany", "forceliterals", "forceplaceholders",
                "forceselectorder", "forcenestedloop", "reverse", "repeatableread",
                "optimisticlock", "pessimisticlock", "validtimestate", "generateonly"
            };

        private static readonly HashSet<string> CompareOps =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "==", "!=", "<", "<=", ">", ">=", "like" };

        // If tokens[k] begins a select clause keyword, return its display text and token
        // length; otherwise null.
        private static void MatchClause(List<Token> t, int k, out string kw, out int len)
        {
            kw = null; len = 0;
            string a = Low(t, k), b = Low(t, k + 1), c = Low(t, k + 2);
            if (a == "from" || a == "where" || a == "join" || a == "having" ||
                a == "exists" || a == "notexists")
            {
                if (a == "exists" || a == "notexists")
                {
                    if (b == "join") { kw = t[k].Value + " join"; len = 2; }
                    return; // 'exists'/'notexists' only start a clause as 'exists join'
                }
                kw = t[k].Value; len = 1; return;
            }
            if ((a == "outer" || a == "inner") && b == "join") { kw = t[k].Value + " join"; len = 2; return; }
            if (a == "full" && b == "outer" && c == "join") { kw = "full outer join"; len = 3; return; }
            if (a == "group" && b == "by") { kw = t[k].Value + " by"; len = 2; return; }
            if (a == "order" && b == "by") { kw = t[k].Value + " by"; len = 2; return; }
        }

        private static string Low(List<Token> t, int i) =>
            i >= 0 && i < t.Count ? t[i].Value.ToLowerInvariant() : "";

        // Split [a,b) into ranges separated by top-level tokens whose value is in `seps`.
        // Returns the ranges; `separators` receives the separator token value before each
        // range (empty for the first).
        private static List<int[]> SplitTopLevel(List<Token> t, int a, int b, HashSet<string> seps,
                                                 List<string> separators)
        {
            var ranges = new List<int[]>();
            int start = a, paren = 0;
            string pendingSep = "";
            for (int k = a; k < b; k++)
            {
                string v = t[k].Value;
                if (t[k].Type == TokenType.Punctuation && (v == "(" || v == "[")) paren++;
                else if (t[k].Type == TokenType.Punctuation && (v == ")" || v == "]")) paren--;
                else if (paren == 0 && seps.Contains(v))
                {
                    ranges.Add(new[] { start, k });
                    separators.Add(pendingSep);
                    pendingSep = v;
                    start = k + 1;
                }
            }
            ranges.Add(new[] { start, b });
            separators.Add(pendingSep);
            return ranges;
        }

        private static readonly HashSet<string> CommaSep = new HashSet<string> { "," };
        private static readonly HashSet<string> ConjSep = new HashSet<string> { "&&", "||" };

        // ---- "chop when multiline" argument lists --------------------------

        // If a parenthesized argument list spans more than one source line, break the line
        // after every top-level comma in it (and mark the '(' so the renderer can indent
        // the continuation). If it is all on one line, it is reflowed to one line. This
        // mirrors the ReSharper/Prettier "chop when multiline" rule the user described.
        private static bool IsWrapOpen(string v) => v == "(" || v == "[";

        // words after which '[' opens a literal (keeps a space) rather than indexing
        private static readonly HashSet<string> BracketLiteralKeywords =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "return", "throw", "case", "in" };

        private static void MarkCommaBreaks(List<Token> tokens)
        {
            var multiline = new bool[tokens.Count];
            var stack = new Stack<int>();
            for (int k = 0; k < tokens.Count; k++)
            {
                Token t = tokens[k];
                if (t.NewlinesBefore >= 1 && stack.Count > 0 && IsWrapOpen(tokens[stack.Peek()].Value))
                    multiline[stack.Peek()] = true;
                if (t.Type == TokenType.Punctuation)
                {
                    if (t.Value == "(" || t.Value == "[") stack.Push(k);
                    else if (t.Value == ")" || t.Value == "]") { if (stack.Count > 0) stack.Pop(); }
                }
            }

            for (int k = 0; k < tokens.Count; k++)
                if (tokens[k].Type == TokenType.Punctuation && IsWrapOpen(tokens[k].Value))
                    tokens[k].GroupMultiline = multiline[k];

            stack = new Stack<int>();
            for (int k = 0; k < tokens.Count; k++)
            {
                Token t = tokens[k];
                if (t.Type != TokenType.Punctuation) continue;
                if (t.Value == ",")
                {
                    if (stack.Count > 0 && IsWrapOpen(tokens[stack.Peek()].Value) && multiline[stack.Peek()])
                        t.BreakAfter = true;
                }
                else if (t.Value == "(" || t.Value == "[") stack.Push(k);
                else if (t.Value == ")" || t.Value == "]") { if (stack.Count > 0) stack.Pop(); }
            }
        }

        // ---- ternary chop --------------------------------------------------

        // A ternary "cond ? a : b" that the author wrote across multiple source lines is
        // chopped so the '?' and ':' each start their own line (indented one level). A
        // ternary that fits on one line is left inline. Matching '?'/':' is LIFO so nested
        // ternaries pair correctly; the stack resets at statement/block boundaries.
        private static void MarkTernaryBreaks(List<Token> t)
        {
            var qStack = new Stack<int>();
            for (int i = 0; i < t.Count; i++)
            {
                Token tk = t[i];
                if (tk.Type == TokenType.Punctuation && (tk.Value == ";" || tk.Value == "{" || tk.Value == "}"))
                {
                    qStack.Clear();
                }
                else if (tk.Type == TokenType.Operator && tk.Value == "?")
                {
                    qStack.Push(i);
                }
                else if (tk.Type == TokenType.Punctuation && tk.Value == ":" && qStack.Count > 0)
                {
                    int q = qStack.Pop();
                    if (TernaryIsMultiline(t, q, i))
                    {
                        t[q].BreakBefore = true;
                        t[i].BreakBefore = true;
                    }
                }
            }
        }

        private static bool TernaryIsMultiline(List<Token> t, int q, int colon)
        {
            int end = Math.Min(colon + 1, t.Count - 1);
            for (int k = q; k <= end; k++)
                if (t[k].NewlinesBefore >= 1) return true;
            return false;
        }

        // ---- Phase A: render ------------------------------------------------

        private sealed class Renderer
        {
            private readonly List<Token> _t;
            private readonly StringBuilder _sb = new StringBuilder();
            private int _depth;      // brace depth
            private int _hanging;    // braceless-body extra indent (reset at statement end)
            private int _paren;      // parenthesis depth
            private bool _lineStart = true;
            private bool _noSpaceAfter;   // previous token was a prefix/postfix unary
            private bool _hangNext;       // next token begins a braceless body
            private Token _prev;
            private readonly Stack<bool> _controlParen = new Stack<bool>();
            private readonly Stack<bool> _wrapParen = new Stack<bool>(); // is this paren a multiline arg list
            private readonly Stack<int> _argCol = new Stack<int>();      // content column of each multiline arg list
            private readonly Stack<int> _ternaryCol = new Stack<int>();  // '=' column each chopped ternary aligns to

            // switch/case: one entry per open brace. _switchBrace[i] = that block is a
            // switch body; _caseActive[i] = we are past a case/default label in it, so its
            // statements get one extra indent level.
            private readonly List<bool> _switchBrace = new List<bool>();
            private readonly List<bool> _caseActive = new List<bool>();
            private readonly List<bool> _caseSeen = new List<bool>(); // a case label already emitted in this switch
            private bool _pendingSwitch;      // the next '{' opens a switch body
            private bool _expectLabelColon;   // the next ':' closes a case/default label

            private readonly bool _inline; // sub-render of an expression: no select handling, no newlines

            public Renderer(List<Token> tokens, bool inline = false) { _t = tokens; _inline = inline; }

            private int CaseExtra()
            {
                int n = 0;
                for (int i = 0; i < _switchBrace.Count; i++)
                    if (_switchBrace[i] && _caseActive[i]) n++;
                return n;
            }

            private int IndentLevel() => _depth + _hanging + CaseExtra();
            private bool TopIsSwitch() => _switchBrace.Count > 0 && _switchBrace[_switchBrace.Count - 1];
            private bool CaseActiveTop() => _caseActive.Count > 0 && _caseActive[_caseActive.Count - 1];
            private void SetCaseActiveTop(bool v) { if (_caseActive.Count > 0) _caseActive[_caseActive.Count - 1] = v; }
            private bool CaseSeenTop() => _caseSeen.Count > 0 && _caseSeen[_caseSeen.Count - 1];
            private void SetCaseSeenTop(bool v) { if (_caseSeen.Count > 0) _caseSeen[_caseSeen.Count - 1] = v; }

            public string Run()
            {
                for (int idx = 0; idx < _t.Count; idx++)
                {
                    _peekNext = idx + 1 < _t.Count ? _t[idx + 1] : null;
                    Token t = _t[idx];
                    Token next = _peekNext;

                    // 1. braceless body: open its own indented line
                    if (_hangNext)
                    {
                        _hangNext = false;
                        _hanging += 1;
                        NewLine();
                    }

                    // 2. preserve blank line between statements (case labels manage their
                    // own separating blank line below, so skip them here)
                    bool tIsCaseLabel = t.Type == TokenType.Word && TopIsSwitch() &&
                        (string.Equals(t.Value, "case", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(t.Value, "default", StringComparison.OrdinalIgnoreCase));
                    if (_lineStart && _prev != null && t.NewlinesBefore >= 2 &&
                        _prev.Value != "{" && t.Value != "}" && !tIsCaseLabel)
                    {
                        TrimLineEnd(); // keep the blank line empty (no trailing indent)
                        _sb.Append('\n');
                        _sb.Append(Indent(IndentLevel()));
                    }

                    // 3. select statement: hand off to the dedicated SQL-style formatter
                    if (!_inline && _lineStart && TryRenderSelect(ref idx))
                    {
                        _prev = _t[idx];
                        continue;
                    }

                    // 4. ternary '?' / ':' on its own line, aligned under the '='
                    if (!_inline && t.BreakBefore && !_lineStart)
                    {
                        int col;
                        if (t.Value == "?") { col = TernaryAlignColumn(); _ternaryCol.Push(col); }
                        else { col = _ternaryCol.Count > 0 ? _ternaryCol.Pop() : Tab * (IndentLevel() + 1); }
                        TrimLineEnd();
                        _sb.Append('\n');
                        _sb.Append(new string(' ', col));
                        _lineStart = true;
                    }

                    bool suppress = _noSpaceAfter;
                    _noSpaceAfter = false;

                    Dispatch(t, next, suppress, ref idx);
                    _prev = _t[idx]; // idx may have advanced past a consumed trailing comment
                }
                return _sb.ToString();
            }

            private bool Dispatch(Token t, Token next, bool suppress, ref int idx)
            {
                switch (t.Type)
                {
                    case TokenType.Directive:
                        if (!_lineStart) NewLine();
                        _sb.Append(t.Value);
                        NewLine();
                        return true;

                    case TokenType.Comment:
                        EmitComment(t);
                        return true;
                }

                switch (t.Value)
                {
                    case "{": EmitOpenBrace(next, ref idx); return true;
                    case "}": EmitCloseBrace(next, ref idx); return true;
                    case ";": EmitSemicolon(next, ref idx); return true;
                    case "(":
                        if (!_inline && IsConjunctionGroup(idx, out int conjClose))
                            EmitConjunctionGroup(t, idx, conjClose, suppress, ref idx);
                        else
                            EmitOpenParen(t, suppress);
                        return true;
                    case ")": EmitCloseParen(next); return true;
                    case "[": EmitOpenBracket(t); return true;
                    case "]": EmitCloseBracket(); return true;
                    case ",": EmitComma(t); return true;
                    case ".": Attach("."); return true;
                    case "::": Attach("::"); return true;
                    case ":":
                        if (_expectLabelColon) EmitLabelColon(next, ref idx);
                        else EmitOtherColon();
                        return true;
                }

                EmitValueOrOperator(t, suppress);
                return true;
            }

            // --- comment ---
            private void EmitComment(Token t)
            {
                string text = NormalizeComment(t.Value);
                if (t.NewlinesBefore == 0 && !_lineStart)
                {
                    if (!EndsWith(' ')) _sb.Append(' ');
                    _sb.Append(text); // trailing, stays on line
                    return;
                }
                if (!_lineStart) NewLine();
                _sb.Append(text); // own line — already at the current indent
                NewLine();
            }

            // Line comments: keep the leading slash run, then exactly one space before the
            // (trimmed) text. Block comments (/* ... */) are left untouched.
            private static string NormalizeComment(string c)
            {
                if (!c.StartsWith("//")) return c;
                int i = 0;
                while (i < c.Length && c[i] == '/') i++;
                string slashes = c.Substring(0, i);
                string rest = c.Substring(i).Trim();
                return rest.Length == 0 ? slashes : slashes + " " + rest;
            }

            // --- braces ---
            private void EmitOpenBrace(Token next, ref int idx)
            {
                if (!_lineStart) NewLine();
                _sb.Append('{');
                _lineStart = false;
                ConsumeTrailingComment(next, ref idx);
                _switchBrace.Add(_pendingSwitch);
                _caseActive.Add(false);
                _caseSeen.Add(false);
                _pendingSwitch = false;
                _depth++;
                NewLine();
            }

            private void EmitCloseBrace(Token next, ref int idx)
            {
                _depth = Math.Max(0, _depth - 1);
                _hanging = 0;
                if (_switchBrace.Count > 0)
                {
                    _switchBrace.RemoveAt(_switchBrace.Count - 1);
                    _caseActive.RemoveAt(_caseActive.Count - 1);
                    _caseSeen.RemoveAt(_caseSeen.Count - 1);
                }
                if (_lineStart) ReindentCurrentLine();
                else NewLine();
                _sb.Append('}');
                _lineStart = false;
                ConsumeTrailingComment(next, ref idx);
                NewLine();
            }

            // --- semicolon ---
            private void EmitSemicolon(Token next, ref int idx)
            {
                if (_paren > 0)
                {
                    Attach(";"); // separator inside (...) e.g. for(;;)
                    return;
                }
                Attach(";");
                ConsumeTrailingComment(next, ref idx);
                _hanging = 0;
                NewLine();
            }

            // --- parentheses ---
            // '[' / ']' participate in the same "chop when multiline" tracking as '(' / ')',
            // so a container/array literal written across lines chops one element per line
            // aligned under the '['.
            private void EmitOpenBracket(Token t)
            {
                // '[' is an index when it follows a value (identifier / ) / ] / number) →
                // tight: arr[i]. Otherwise it opens a container/array literal and keeps the
                // preceding spacing: '= [', 'return [', ', ['.
                bool index = _prev != null &&
                    (_prev.Type == TokenType.Number || _prev.Value == ")" || _prev.Value == "]" ||
                     (_prev.Type == TokenType.Word && !BracketLiteralKeywords.Contains(_prev.Value)));
                if (index)
                {
                    Attach("[");
                }
                else
                {
                    if (!_lineStart && NeedsSpaceBeforeValue()) _sb.Append(' ');
                    _sb.Append('[');
                    _lineStart = false;
                }
                _wrapParen.Push(t.GroupMultiline);
                if (t.GroupMultiline) _argCol.Push(CurrentColumn());
            }

            private void EmitCloseBracket()
            {
                if (_wrapParen.Count > 0 && _wrapParen.Pop()) { if (_argCol.Count > 0) _argCol.Pop(); }
                Attach("]");
            }

            private void EmitComma(Token t)
            {
                Attach(",");
                if (t.BreakAfter)
                {
                    // align the next argument under the opening '(' (aligned-argument style)
                    _sb.Append('\n');
                    int col = _argCol.Count > 0 ? _argCol.Peek() : Indent(IndentLevel() + 1).Length;
                    _sb.Append(new string(' ', col));
                    _lineStart = true;
                }
                else
                {
                    _sb.Append(' ');
                    _lineStart = false;
                }
            }

            // number of characters on the current (unfinished) line
            private int CurrentColumn()
            {
                int nl = -1;
                for (int i = _sb.Length - 1; i >= 0; i--)
                    if (_sb[i] == '\n') { nl = i; break; }
                return _sb.Length - (nl + 1);
            }

            // column of the assignment '=' on the current line (so a chopped ternary's
            // '?' / ':' line up under it); falls back to one indent level in.
            private int TernaryAlignColumn()
            {
                int lineStart = _sb.Length;
                while (lineStart > 0 && _sb[lineStart - 1] != '\n') lineStart--;
                for (int k = lineStart; k + 2 < _sb.Length; k++)
                    if (_sb[k] == ' ' && _sb[k + 1] == '=' && _sb[k + 2] == ' ')
                        return (k + 1) - lineStart;
                return Tab * (IndentLevel() + 1);
            }

            private void EmitOpenParen(Token t, bool suppress)
            {
                bool control = _prev != null && _prev.Type == TokenType.Word &&
                               IsControlKeyword(_prev.Value);
                // Keywords that take a parenthesized expression want a space before '(' —
                // control keywords plus return/throw. A plain identifier before '(' is a
                // call and gets no space.
                bool spaceKeyword = control || (_prev != null && _prev.Type == TokenType.Word &&
                                                SpaceBeforeParenKeywords.Contains(_prev.Value));
                if (spaceKeyword && !_lineStart && !suppress)
                {
                    if (!EndsWith(' ')) _sb.Append(' ');
                    _sb.Append('(');
                }
                else
                {
                    Attach("(");
                }
                _lineStart = false;
                _paren++;
                _controlParen.Push(control); // only true control keywords drive braceless bodies
                _wrapParen.Push(t.GroupMultiline);
                if (t.GroupMultiline) _argCol.Push(CurrentColumn()); // column just after '('
            }

            private void EmitCloseParen(Token next)
            {
                if (_wrapParen.Count > 0 && _wrapParen.Pop()) { if (_argCol.Count > 0) _argCol.Pop(); }
                Attach(")");
                _paren = Math.Max(0, _paren - 1);
                bool wasControl = _controlParen.Count > 0 && _controlParen.Pop();
                if (wasControl && _paren == 0 && next != null &&
                    next.Value != "{" && next.Value != ";")
                {
                    _hangNext = true;
                }
            }

            // --- multiline boolean condition ("chop when multiline") ---
            //
            // A parenthesized boolean expression the author wrote across several source
            // lines is chopped so each top-level && / || starts its own line, with the
            // operands aligned under the first operand and (where every row is a simple
            // lhs OP rhs) the compare operators aligned in a column — the same layout the
            // SELECT 'where' clause uses. Only fires for a *pure* boolean paren: no
            // top-level ',' or ';' (so argument lists and for-headers are untouched) and
            // no comment inside (which would break the single-line-per-row assumption).
            private bool IsConjunctionGroup(int openIdx, out int closeIdx)
            {
                closeIdx = -1;
                int depth = 0, close = -1;
                for (int j = openIdx; j < _t.Count; j++)
                {
                    if (_t[j].Type != TokenType.Punctuation) continue;
                    string pv = _t[j].Value;
                    if (pv == "(" || pv == "[") depth++;
                    else if (pv == ")" || pv == "]") { depth--; if (depth == 0) { close = j; break; } }
                }
                if (close < 0) return false;

                bool multiline = _t[close].NewlinesBefore >= 1;
                bool hasConj = false;
                int d = 0;
                for (int j = openIdx + 1; j < close; j++)
                {
                    if (_t[j].NewlinesBefore >= 1) multiline = true;
                    if (_t[j].Type == TokenType.Comment) return false;
                    string v = _t[j].Value;
                    if (_t[j].Type == TokenType.Punctuation && (v == "(" || v == "[")) { d++; continue; }
                    if (_t[j].Type == TokenType.Punctuation && (v == ")" || v == "]")) { d--; continue; }
                    if (d != 0) continue;
                    if (v == "," || v == ";") return false;                 // arg list / for-header
                    if (_t[j].Type == TokenType.Operator && (v == "&&" || v == "||")) hasConj = true;
                }
                if (!multiline || !hasConj) return false;
                closeIdx = close;
                return true;
            }

            private void EmitConjunctionGroup(Token open, int openIdx, int closeIdx, bool suppress, ref int idx)
            {
                // '(' with the same spacing rules as EmitOpenParen
                bool control = _prev != null && _prev.Type == TokenType.Word && IsControlKeyword(_prev.Value);
                bool spaceKeyword = control || (_prev != null && _prev.Type == TokenType.Word &&
                                                SpaceBeforeParenKeywords.Contains(_prev.Value));
                if (spaceKeyword && !_lineStart && !suppress)
                {
                    if (!EndsWith(' ')) _sb.Append(' ');
                    _sb.Append('(');
                }
                else
                {
                    Attach("(");
                }
                _lineStart = false;

                int contentCol = CurrentColumn();          // operands align here (just after '(')
                int baseIndent = Tab * IndentLevel();       // the statement's own indent
                if (baseIndent > contentCol) baseIndent = Math.Max(0, contentCol - 1);

                var conjs = new List<string>();
                var conds = SplitTopLevel(_t, openIdx + 1, closeIdx, ConjSep, conjs);

                var left = new List<string>();
                var ops = new List<string>();
                var right = new List<string>();
                foreach (var r in conds)
                {
                    int oi = FindCompareOp(r[0], r[1]);
                    if (oi < 0) { left.Add(Inline(_t, r[0], r[1])); ops.Add(""); right.Add(""); }
                    else { left.Add(Inline(_t, r[0], oi)); ops.Add(_t[oi].Value); right.Add(Inline(_t, oi + 1, r[1])); }
                }
                int maxLeft = 0;
                for (int k = 0; k < conds.Count; k++)
                    if (ops[k].Length > 0) maxLeft = Math.Max(maxLeft, left[k].Length);

                for (int k = 0; k < conds.Count; k++)
                {
                    string cond = ops[k].Length > 0
                        ? left[k].PadRight(maxLeft) + " " + ops[k] + " " + right[k]
                        : left[k];
                    if (k == 0)
                    {
                        _sb.Append(cond);
                    }
                    else
                    {
                        _sb.Append('\n');
                        string conj = conjs[k];
                        int pad = contentCol - baseIndent - conj.Length;
                        if (pad < 1) pad = 1;
                        _sb.Append(new string(' ', baseIndent));
                        _sb.Append(conj);
                        _sb.Append(new string(' ', pad));
                        _sb.Append(cond);
                    }
                }
                _lineStart = false;

                Attach(")");

                Token afterClose = closeIdx + 1 < _t.Count ? _t[closeIdx + 1] : null;
                if (control && afterClose != null && afterClose.Value != "{" && afterClose.Value != ";")
                    _hangNext = true;

                idx = closeIdx; // main loop's idx++ lands past ')'
            }

            // --- colons ---
            private void EmitLabelColon(Token next, ref int idx)
            {
                Attach(":");               // case 0:  (no space before)
                _expectLabelColon = false;
                if (TopIsSwitch()) SetCaseActiveTop(true); // body indents one level
                ConsumeTrailingComment(next, ref idx);
                NewLine();
            }

            private void EmitOtherColon()
            {
                // ternary / general colon: surround with single spaces
                if (!_lineStart && !EndsWith(' ')) _sb.Append(' ');
                _sb.Append(": ");
                _lineStart = false;
            }

            // --- value / operator ---
            private void EmitValueOrOperator(Token t, bool suppress)
            {
                string v = t.Value;

                // switch / case bookkeeping
                if (t.Type == TokenType.Word)
                {
                    string lwk = v.ToLowerInvariant();
                    if (lwk == "switch")
                    {
                        _pendingSwitch = true;
                    }
                    else if ((lwk == "case" || lwk == "default") && TopIsSwitch())
                    {
                        if (!_lineStart) NewLine();
                        SetCaseActiveTop(false); // the label sits at the switch-brace level
                        if (CaseSeenTop())
                        {
                            // one blank line between case blocks (not before the first)
                            TrimLineEnd();
                            _sb.Append('\n');
                            _sb.Append(Indent(IndentLevel()));
                            _lineStart = true;
                        }
                        else
                        {
                            ReindentCurrentLine();
                        }
                        SetCaseSeenTop(true);
                        _expectLabelColon = true;
                    }
                }

                // postfix/prefix ++ -- : no surrounding space
                if (t.Type == TokenType.Operator && (v == "++" || v == "--"))
                {
                    if (!_lineStart) TrimLineEnd();
                    _sb.Append(v);
                    _lineStart = false;
                    _noSpaceAfter = true;
                    return;
                }

                // unary ! ~ and unary + - : space before depends on context, none after
                if (IsUnaryHere(t))
                {
                    bool noSpaceCtx = _prev != null &&
                        (_prev.Value == "(" || _prev.Value == "[" || _prev.Value == "{" ||
                         _prev.Value == "." || _prev.Value == "::");
                    if (!_lineStart && !suppress && !noSpaceCtx)
                    {
                        if (!EndsWith(' ')) _sb.Append(' ');
                    }
                    else if (!_lineStart)
                    {
                        TrimLineEnd();
                    }
                    _sb.Append(v);
                    _lineStart = false;
                    _noSpaceAfter = true;
                    return;
                }

                if (t.Type == TokenType.Operator)
                {
                    // binary operator: single space either side
                    if (!_lineStart && !suppress && !EndsWith(' '))
                        _sb.Append(' ');
                    _sb.Append(v);
                    _sb.Append(' ');
                    _lineStart = false;
                    return;
                }

                // word / number / string
                bool attachToPrev = _prev != null && (_prev.Value == "." || _prev.Value == "::");
                if (!_lineStart && !suppress && !attachToPrev && NeedsSpaceBeforeValue())
                    _sb.Append(' ');
                _sb.Append(v);
                _lineStart = false;

                if (t.Type == TokenType.Word)
                {
                    string lw = v.ToLowerInvariant();
                    if (lw == "else")
                    {
                        Token nx = PeekNextSignificant();
                        if (nx != null && nx.Value != "{" &&
                            !(nx.Type == TokenType.Word &&
                              string.Equals(nx.Value, "if", StringComparison.OrdinalIgnoreCase)))
                            _hangNext = true;
                    }
                    else if (lw == "do")
                    {
                        Token nx = PeekNextSignificant();
                        if (nx != null && nx.Value != "{") _hangNext = true;
                    }
                }
            }

            private bool IsUnaryHere(Token t)
            {
                if (t.Type != TokenType.Operator) return false;
                string v = t.Value;
                if (v == "!" || v == "~") return true;
                if (v == "+" || v == "-")
                {
                    bool ctx = _prev == null || _prev.Type == TokenType.Operator ||
                               _prev.Value == "(" || _prev.Value == "[" || _prev.Value == "{" ||
                               _prev.Value == "," || _prev.Value == ";" ||
                               (_prev.Type == TokenType.Word && IsUnaryPrecedingKeyword(_prev.Value));
                    return ctx;
                }
                return false;
            }

            // A '#' cache to find the next non-consumed token for else/do lookahead.
            private Token PeekNextSignificant()
            {
                // _prev is set after this token; the "next" is simply the following item.
                // We approximate via the renderer's token list position by scanning from _prev.
                // Simpler: the caller already knows next through Run(); but else/do rarely
                // need more than the immediate next token, which equals _t[current+1].
                // We locate current by reference identity.
                return _peekNext;
            }

            private Token _peekNext;

            // --- helpers ---
            private void NewLine()
            {
                TrimLineEnd();
                _sb.Append('\n');
                _sb.Append(Indent(IndentLevel()));
                _lineStart = true;
            }

            // Rewrite the current (indent-only) line's leading spaces to the current
            // level — used when a closing brace or a case label lowers the indent after
            // it was already emitted.
            private void ReindentCurrentLine()
            {
                int end = _sb.Length;
                while (end > 0 && _sb[end - 1] == ' ') end--;
                _sb.Length = end;
                _sb.Append(Indent(IndentLevel()));
                _lineStart = true;
            }

            private void TrimLineEnd()
            {
                int end = _sb.Length;
                while (end > 0 && _sb[end - 1] == ' ') end--;
                _sb.Length = end;
            }

            private void Attach(string s)
            {
                if (!_lineStart) TrimLineEnd();
                _sb.Append(s);
                _lineStart = false;
            }

            private bool EndsWith(char c) => _sb.Length > 0 && _sb[_sb.Length - 1] == c;

            private bool NeedsSpaceBeforeValue()
            {
                if (_sb.Length == 0) return false;
                char last = _sb[_sb.Length - 1];
                return last != ' ' && last != '(' && last != '[' && last != '.' && last != '{';
            }

            private void ConsumeTrailingComment(Token next, ref int idx)
            {
                if (next != null && next.Type == TokenType.Comment && next.NewlinesBefore == 0)
                {
                    _sb.Append(' ');
                    _sb.Append(NormalizeComment(next.Value));
                    idx++; // consume it
                }
            }

            // ---- select statement: SQL-style multi-column layout ----
            //
            //   while                         <- (only for 'while select')
            //       select <mods> <fields>
            //       from   <op>               <- operands align to the field column
            //
            //       join   <table>            <- blank line before each join
            //       where  <lhs> == <rhs>     <- comparison operators aligned within a where
            //           && <lhs2> == <rhs2>   <- && right-aligned so conditions line up
            //
            // Returns false (fall back to normal rendering) if the statement is malformed.
            private bool TryRenderSelect(ref int idx)
            {
                int i = idx;
                bool hasWhile = _t[i].Type == TokenType.Word && Eq(_t[i].Value, "while") &&
                                i + 1 < _t.Count && _t[i + 1].Type == TokenType.Word && Eq(_t[i + 1].Value, "select");
                if (!hasWhile && !(_t[i].Type == TokenType.Word && Eq(_t[i].Value, "select")))
                    return false;

                int selIdx = hasWhile ? i + 1 : i;

                int paren = 0, end = -1;
                for (int j = selIdx + 1; j < _t.Count; j++)
                {
                    if (_t[j].Type != TokenType.Punctuation) continue;
                    string v = _t[j].Value;
                    if (v == "(" || v == "[") paren++;
                    else if (v == ")" || v == "]") paren--;
                    else if (paren == 0 && (v == ";" || v == "{")) { end = j; break; }
                }
                if (end < 0) return false;

                int lvl = IndentLevel();
                string baseIndent = Indent(lvl);
                string kwIndent = hasWhile ? Indent(lvl + 1) : baseIndent;

                var b = new StringBuilder();
                if (hasWhile) { b.Append(baseIndent); b.Append("while"); b.Append('\n'); }

                int p = selIdx + 1;
                var mods = new List<string>();
                while (p < end && _t[p].Type == TokenType.Word &&
                       SelectModifiers.Contains(_t[p].Value.ToLowerInvariant()))
                { mods.Add(_t[p].Value); p++; }

                int clauseStart = FindClause(p, end);
                int fieldsEnd = clauseStart < 0 ? end : clauseStart;

                string selectPrefix = kwIndent + "select" +
                    (mods.Count > 0 ? " " + string.Join(" ", mods) : "") + " ";
                int fieldCol = selectPrefix.Length;

                b.Append(selectPrefix);
                var seps = new List<string>();
                var fields = SplitTopLevel(_t, p, fieldsEnd, CommaSep, seps);
                bool firstField = true;
                for (int f = 0; f < fields.Count; f++)
                {
                    string txt = Inline(_t, fields[f][0], fields[f][1]);
                    if (txt.Length == 0) continue;
                    if (!firstField) { b.Append('\n'); b.Append(new string(' ', fieldCol)); }
                    b.Append(txt);
                    if (f < fields.Count - 1) b.Append(',');
                    firstField = false;
                }

                int c = clauseStart;
                while (c >= 0 && c < end)
                {
                    MatchClause(_t, c, out string kw, out int klen);
                    if (kw == null) break;
                    int opStart = c + klen;
                    int oEnd = FindClause(opStart, end);
                    if (oEnd < 0) oEnd = end;

                    b.Append('\n');
                    if (kw.ToLowerInvariant().EndsWith("join")) b.Append('\n'); // blank line before a join

                    if (Eq(kw, "where"))
                        b.Append(RenderWhere(opStart, oEnd, kwIndent, fieldCol));
                    else
                        b.Append(PadKeyword(kwIndent, kw, fieldCol) + Inline(_t, opStart, oEnd));

                    c = oEnd < end ? oEnd : -1;
                }

                TrimLineEnd();
                _sb.Append(b.ToString());
                _lineStart = false;
                idx = end - 1; // the for-loop's idx++ lands on the terminator (handled normally)
                return true;
            }

            private int FindClause(int from, int to)
            {
                int paren = 0;
                for (int k = from; k < to; k++)
                {
                    string v = _t[k].Value;
                    if (_t[k].Type == TokenType.Punctuation && (v == "(" || v == "[")) { paren++; continue; }
                    if (_t[k].Type == TokenType.Punctuation && (v == ")" || v == "]")) { paren--; continue; }
                    if (paren != 0) continue;
                    MatchClause(_t, k, out string kw, out int len);
                    if (kw != null) return k;
                }
                return -1;
            }

            private static string PadKeyword(string indent, string kw, int fieldCol)
            {
                string s = indent + kw;
                return s.Length < fieldCol ? s + new string(' ', fieldCol - s.Length) : s + " ";
            }

            private string RenderWhere(int a, int b, string kwIndent, int fieldCol)
            {
                var conjs = new List<string>();
                var conds = SplitTopLevel(_t, a, b, ConjSep, conjs);

                var left = new List<string>();
                var op = new List<string>();
                var right = new List<string>();
                foreach (var r in conds)
                {
                    int oi = FindCompareOp(r[0], r[1]);
                    if (oi < 0) { left.Add(Inline(_t, r[0], r[1])); op.Add(""); right.Add(""); }
                    else { left.Add(Inline(_t, r[0], oi)); op.Add(_t[oi].Value); right.Add(Inline(_t, oi + 1, r[1])); }
                }

                int maxLeft = 0;
                for (int k = 0; k < left.Count; k++)
                    if (op[k].Length > 0) maxLeft = Math.Max(maxLeft, left[k].Length);

                var sb = new StringBuilder();
                for (int k = 0; k < conds.Count; k++)
                {
                    string cond = op[k].Length > 0
                        ? left[k].PadRight(maxLeft) + " " + op[k] + " " + right[k]
                        : left[k];
                    if (k == 0)
                        sb.Append(PadKeyword(kwIndent, "where", fieldCol) + cond);
                    else
                    {
                        sb.Append('\n');
                        string conj = conjs[k];
                        int pad = fieldCol - (conj.Length + 1);
                        sb.Append(new string(' ', Math.Max(0, pad)) + conj + " " + cond);
                    }
                }
                return sb.ToString();
            }

            private int FindCompareOp(int a, int b)
            {
                int paren = 0;
                for (int k = a; k < b; k++)
                {
                    string v = _t[k].Value;
                    if (_t[k].Type == TokenType.Punctuation && (v == "(" || v == "[")) { paren++; continue; }
                    if (_t[k].Type == TokenType.Punctuation && (v == ")" || v == "]")) { paren--; continue; }
                    if (paren == 0 && CompareOps.Contains(v)) return k;
                }
                return -1;
            }

            private static string Inline(List<Token> src, int a, int b)
            {
                var sub = new List<Token>();
                for (int k = a; k < b; k++)
                {
                    if (src[k].Type == TokenType.Space) continue;
                    sub.Add(new Token(src[k].Type, src[k].Value));
                }
                if (sub.Count == 0) return "";
                return new Renderer(sub, inline: true).Run().Trim();
            }

            private static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static readonly HashSet<string> ControlKeywords =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "if", "for", "while", "switch", "catch" };

        private static bool IsControlKeyword(string w) => ControlKeywords.Contains(w);

        // keywords (other than the control ones) that keep a space before a following '('
        private static readonly HashSet<string> SpaceBeforeParenKeywords =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "return", "throw" };

        private static readonly HashSet<string> UnaryPrecedingKeywords =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "return", "case", "in" };

        private static bool IsUnaryPrecedingKeyword(string w) => UnaryPrecedingKeywords.Contains(w);

        private static string Indent(int level) => new string(' ', Tab * Math.Max(0, level));

        // ---- Phase B: column alignment -------------------------------------

        private static readonly Regex DeclRe = new Regex(
            @"^(?<indent>[ \t]*)(?<type>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*(?:\s*<[^;{}]*>)?(?:\s*\[\s*\])?)[ \t]+" +
            @"(?<name>[A-Za-z_][A-Za-z0-9_]*)[ \t]*" +
            @"(?:(?<eq>(?<![=<>!+\-*/%&|^])=(?!=))[ \t]*(?<rhs>[^;]*?))?[ \t]*;[ \t]*(?<comment>//.*)?$",
            RegexOptions.Compiled);

        private static readonly Regex AssignRe = new Regex(
            @"^(?<indent>[ \t]*)(?<lhs>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*|\[[^\]]*\])*)[ \t]*" +
            @"(?<![=<>!+\-*/%&|^])=(?!=)[ \t]*(?<rhs>[^;]*?)[ \t]*;[ \t]*(?<comment>//.*)?$",
            RegexOptions.Compiled);

        private static readonly HashSet<string> NotAType =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "return", "if", "else", "for", "while", "do", "switch", "case", "default",
                "try", "catch", "finally", "throw", "using", "select", "print", "pause",
                "continue", "break", "retry", "changecompany", "delete_from", "update_recordset",
                "insert_recordset", "new", "this", "super"
            };

        // Comment-only lines are always transparent to alignment. With this false, blank
        // lines are transparent too, so a whole contiguous declaration/assignment block
        // aligns to one column (widest type across the block). True = blank lines separate
        // independent alignment sub-groups.
        private const bool BlanksBreakAlignment = false;

        // Start a new alignment group when one line's align width is BOTH more than
        // WidthSplitRatio times another's AND at least WidthSplitAbs chars wider — keeps
        // short types (int) from being dragged wide by a long one (System.Windows.Controls
        // .ItemCollection), while not splitting merely moderate differences (int vs SalesTable).
        private const int WidthSplitRatio = 3;
        private const int WidthSplitAbs = 10;

        // line kinds
        private const int KOther = 0, KDecl = 1, KAssign = 2, KComment = 3, KBlank = 4;

        // the width that drives alignment for a line: the type for a declaration, the lhs
        // for an assignment.
        private static int AlignWidth(int kind, Match m) =>
            kind == KDecl ? Collapse(m.Groups["type"].Value).Length : m.Groups["lhs"].Value.Length;

        private static string AlignColumns(string text)
        {
            string[] lines = text.Split('\n');
            int n = lines.Length;
            var kind = new int[n];
            var indent = new string[n];
            var match = new Match[n];

            for (int i = 0; i < n; i++)
            {
                string raw = lines[i];
                if (raw.Trim().Length == 0) { kind[i] = KBlank; continue; }
                if (raw.TrimStart().StartsWith("//")) { kind[i] = KComment; continue; }

                Match d = DeclRe.Match(raw);
                if (d.Success && !NotAType.Contains(FirstWord(d.Groups["type"].Value)))
                {
                    kind[i] = KDecl; indent[i] = d.Groups["indent"].Value; match[i] = d; continue;
                }
                Match a = AssignRe.Match(raw);
                if (a.Success && !NotAType.Contains(FirstWord(a.Groups["lhs"].Value)))
                {
                    kind[i] = KAssign; indent[i] = a.Groups["indent"].Value; match[i] = a; continue;
                }
                kind[i] = KOther;
            }

            // Assign a group id to each decl/assign line. A group extends over comment
            // lines (and blank lines when BlanksBreakAlignment is false) that separate
            // same-kind, same-indent lines.
            var gid = new int[n];
            for (int i = 0; i < n; i++) gid[i] = -1;
            int g = 0;
            for (int i = 0; i < n; i++)
            {
                if ((kind[i] != KDecl && kind[i] != KAssign) || gid[i] != -1) continue;
                int k0 = kind[i];
                string ind0 = indent[i];
                gid[i] = g;
                int groupW = AlignWidth(kind[i], match[i]);
                for (int j = i + 1; j < n; j++)
                {
                    if (kind[j] == k0 && indent[j] == ind0)
                    {
                        // split the group when the align widths differ a lot, so short
                        // types don't get dragged wide by a long one (and vice-versa)
                        int w = AlignWidth(kind[j], match[j]);
                        int lo = Math.Min(w, groupW), hi = Math.Max(w, groupW);
                        if (lo > 0 && hi > lo * WidthSplitRatio && hi - lo >= WidthSplitAbs) break;
                        groupW = Math.Max(groupW, w);
                        gid[j] = g;
                    }
                    else if (kind[j] == KComment) { /* transparent */ }
                    else if (kind[j] == KBlank && !BlanksBreakAlignment) { /* transparent */ }
                    else break;
                }
                g++;
            }

            var members = new Dictionary<int, List<int>>();
            for (int i = 0; i < n; i++)
                if (gid[i] >= 0)
                {
                    if (!members.TryGetValue(gid[i], out var l)) { l = new List<int>(); members[gid[i]] = l; }
                    l.Add(i);
                }

            var outp = new string[n];
            for (int i = 0; i < n; i++) outp[i] = lines[i];

            foreach (var kv in members)
            {
                var idxs = kv.Value;
                if (kind[idxs[0]] == KDecl) RenderDeclGroup(idxs, match, outp);
                else RenderAssignGroup(idxs, match, outp);
            }

            return string.Join("\n", outp);
        }

        private static void RenderDeclGroup(List<int> idxs, Match[] match, string[] outp)
        {
            int typeW = 0, nameW = 0;
            foreach (int i in idxs)
            {
                typeW = Math.Max(typeW, Collapse(match[i].Groups["type"].Value).Length);
                nameW = Math.Max(nameW, match[i].Groups["name"].Value.Length);
            }

            var codes = new Dictionary<int, string>();
            int codeW = 0;
            bool anyComment = false;
            foreach (int i in idxs)
            {
                Match m = match[i];
                string ind = m.Groups["indent"].Value;
                string type = Collapse(m.Groups["type"].Value);
                string name = m.Groups["name"].Value;
                string code = m.Groups["eq"].Success
                    ? ind + type.PadRight(typeW) + " " + name.PadRight(nameW) + " = " + m.Groups["rhs"].Value.Trim() + ";"
                    : ind + type.PadRight(typeW) + " " + name + ";";
                codes[i] = code;
                if (m.Groups["comment"].Success) { anyComment = true; codeW = Math.Max(codeW, code.Length); }
            }

            foreach (int i in idxs)
                outp[i] = anyComment && match[i].Groups["comment"].Success
                    ? codes[i].PadRight(codeW) + "  " + match[i].Groups["comment"].Value.TrimEnd()
                    : codes[i];
        }

        private static void RenderAssignGroup(List<int> idxs, Match[] match, string[] outp)
        {
            int lhsW = 0;
            foreach (int i in idxs)
                lhsW = Math.Max(lhsW, match[i].Groups["lhs"].Value.Length);

            var codes = new Dictionary<int, string>();
            int codeW = 0;
            bool anyComment = false;
            foreach (int i in idxs)
            {
                Match m = match[i];
                string code = m.Groups["indent"].Value + m.Groups["lhs"].Value.PadRight(lhsW) +
                              " = " + m.Groups["rhs"].Value.Trim() + ";";
                codes[i] = code;
                if (m.Groups["comment"].Success) { anyComment = true; codeW = Math.Max(codeW, code.Length); }
            }

            foreach (int i in idxs)
                outp[i] = anyComment && match[i].Groups["comment"].Success
                    ? codes[i].PadRight(codeW) + "  " + match[i].Groups["comment"].Value.TrimEnd()
                    : codes[i];
        }

        private static string FirstWord(string s)
        {
            int i = 0;
            while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
            return s.Substring(0, i);
        }

        // collapse internal whitespace in a captured type (e.g. "List < str >" -> "List<str>")
        private static string Collapse(string s) => Regex.Replace(s.Trim(), @"\s+", "");

        private static int ClassifyKind(string raw)
        {
            if (raw.Trim().Length == 0) return KBlank;
            if (raw.TrimStart().StartsWith("//")) return KComment;
            Match d = DeclRe.Match(raw);
            if (d.Success && !NotAType.Contains(FirstWord(d.Groups["type"].Value))) return KDecl;
            Match a = AssignRe.Match(raw);
            if (a.Success && !NotAType.Contains(FirstWord(a.Groups["lhs"].Value))) return KAssign;
            return KOther;
        }

        // Ensure a blank line separates a block of local declarations from the code that
        // follows it. Does nothing if a blank line (or the traditional lone ';' empty
        // statement plus a blank) is already there. Idempotent.
        private static string EnsureBlankAfterDeclarations(string text)
        {
            var lines = new List<string>(text.Split('\n'));
            int n = lines.Count;
            var kind = new int[n];
            for (int i = 0; i < n; i++) kind[i] = ClassifyKind(lines[i]);

            var inserts = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (kind[i] != KDecl) continue;
                // first real code line after i, skipping comments, blanks and a lone ';'
                int j = i + 1;
                while (j < n && (kind[j] == KComment || kind[j] == KBlank || lines[j].Trim() == ";")) j++;
                if (j >= n || kind[j] == KDecl) continue;          // more declarations follow
                if (lines[j].TrimStart().StartsWith("}")) continue; // block closes, no code
                bool hasBlank = false;
                for (int k = i + 1; k < j; k++) if (kind[k] == KBlank) { hasBlank = true; break; }
                if (hasBlank) continue;
                inserts.Add(i + 1 < j && lines[i + 1].Trim() == ";" ? i + 1 : i);
            }

            for (int m = inserts.Count - 1; m >= 0; m--)
                lines.Insert(inserts[m] + 1, "");
            return string.Join("\n", lines);
        }

        // ---- safety verifier ------------------------------------------------

        private static bool TokenStreamsEqual(string a, string b)
        {
            try
            {
                return Comparable(XppLexer.Tokenize(a)) == Comparable(XppLexer.Tokenize(b));
            }
            catch (XppFormatException)
            {
                return false;
            }
        }

        private static string Comparable(List<Token> tokens)
        {
            var sb = new StringBuilder();
            foreach (var t in tokens)
            {
                if (t.Type == TokenType.Space) continue;
                sb.Append((int)t.Type);
                sb.Append(':');
                string cmp;
                if (t.Type == TokenType.Word)
                    cmp = t.Value.ToLowerInvariant();
                else if (t.Type == TokenType.Comment)
                    cmp = Regex.Replace(t.Value, @"\s+", ""); // ignore ALL comment whitespace
                                                              // (so '//x' and '// x' are equivalent)
                else
                    cmp = t.Value;
                sb.Append(cmp);
                sb.Append('\u001f');
            }
            return sb.ToString();
        }
    }
}
