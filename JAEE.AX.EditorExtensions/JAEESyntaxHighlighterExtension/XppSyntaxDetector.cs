using System.Collections.Generic;
using JAEE.AX.EditorExtensions.Format;

namespace JAEE.AX.EditorExtensions
{
    /// <summary>
    /// A detected coloring span: character offset, length, and which category.
    /// Deliberately a plain struct (not a ValueTuple) so this assembly has no
    /// dependency on System.ValueTuple / mscorlib-hosted tuples — the AX client
    /// runtime may resolve ValueTuple differently and fail to load the assembly.
    /// </summary>
    internal struct DetectedSpan
    {
        public int Start;
        public int Length;
        public HighlightCategory Category;

        public DetectedSpan(int start, int length, HighlightCategory category)
        {
            Start = start;
            Length = length;
            Category = category;
        }
    }

    /// <summary>
    /// Pure (no VS deps) detection of X++ Type / Macro / Method spans.
    /// Input: raw source text. Output: list of (start, length, category) triples.
    /// Precedence: Macro > Type > Method.
    /// </summary>
    internal static class XppSyntaxDetector
    {
        public static List<DetectedSpan> Classify(string text)
        {
            var results = new List<DetectedSpan>();
            if (string.IsNullOrEmpty(text)) return results;

            List<Token> all;
            try { all = XppLexer.Tokenize(text); }
            catch { return results; }

            // Significant tokens: skip spaces; keep comments (they carry SourceStart for offset)
            // but comments are never colored — we just need them out of the way for pattern matching.
            var sig = new List<Token>();
            foreach (var t in all)
                if (t.Type != TokenType.Space && t.Type != TokenType.Comment)
                    sig.Add(t);

            var covered = new HashSet<int>(); // sig indices already emitted as Macro or Type

            // ---- Pass 1: Macro and Type ----
            int i = 0;
            while (i < sig.Count)
            {
                var t = sig[i];

                // MACRO: whole-line directive
                if (t.Type == TokenType.Directive)
                {
                    results.Add(new DetectedSpan(t.SourceStart, t.Value.Length, HighlightCategory.Macro));
                    covered.Add(i);
                    i++;
                    continue;
                }

                // MACRO: inline #name reference
                if (t.Type == TokenType.Word && t.Value.Length > 1 && t.Value[0] == '#')
                {
                    results.Add(new DetectedSpan(t.SourceStart, t.Value.Length, HighlightCategory.Macro));
                    covered.Add(i);
                    i++;
                    continue;
                }

                // TYPE checks — only for Word tokens
                if (t.Type == TokenType.Word)
                {
                    // Primitive/system keyword → always a Type regardless of position
                    if (IsPrimitive(t.Value))
                    {
                        results.Add(new DetectedSpan(t.SourceStart, t.Value.Length, HighlightCategory.Type));
                        covered.Add(i);
                        i++;
                        continue;
                    }

                    // A language keyword (if, return, new, is, as, ...) is never a type
                    // name and must never start a type chain — otherwise e.g. "x as Foo;"
                    // would match the declaration pattern with "as" as the type.
                    if (IsLanguageKeyword(t.Value)) { i++; continue; }

                    int chainEnd = CollectChain(sig, i);
                    bool emitType = false;

                    // Pattern: <chain> :: → enum/static class reference
                    if (!emitType && chainEnd + 1 < sig.Count && sig[chainEnd + 1].Value == "::")
                        emitType = true;

                    // Pattern: new <chain> → constructor;  is/as <chain> → type-check / cast
                    if (!emitType && i > 0 &&
                        (sig[i - 1].Value == "new" || sig[i - 1].Value == "is" || sig[i - 1].Value == "as"))
                        emitType = true;

                    // Pattern: <chain> [] → array type
                    if (!emitType && chainEnd + 2 < sig.Count &&
                        sig[chainEnd + 1].Value == "[" && sig[chainEnd + 2].Value == "]")
                        emitType = true;

                    // Pattern: <chain> <name> ( ; = , ) → declaration or return type
                    if (!emitType && chainEnd + 1 < sig.Count)
                    {
                        var afterChain = sig[chainEnd + 1];
                        if (afterChain.Type == TokenType.Word && !IsLanguageKeyword(afterChain.Value))
                        {
                            int afterVarIdx = chainEnd + 2;
                            if (afterVarIdx < sig.Count)
                            {
                                string follow = sig[afterVarIdx].Value;
                                if (follow == ";" || follow == "=" || follow == "," ||
                                    follow == ")" || follow == "(")
                                    emitType = true;
                            }
                        }
                    }

                    if (emitType)
                    {
                        var first = sig[i];
                        var last  = sig[chainEnd];
                        int spanLen = last.SourceStart + last.Value.Length - first.SourceStart;
                        results.Add(new DetectedSpan(first.SourceStart, spanLen, HighlightCategory.Type));
                        for (int k = i; k <= chainEnd; k++) covered.Add(k);
                        i = chainEnd + 1;
                        continue;
                    }
                }

                i++;
            }

            // ---- Pass 2: Method ----
            // A Word directly followed by '(' is a call. Preceded by '.' or '::' it is an
            // instance/static member call (Method); otherwise it is a free/global function
            // call (MethodGlobal), e.g. info(), strFmt().
            for (int j = 0; j < sig.Count; j++)
            {
                if (covered.Contains(j)) continue;
                var t = sig[j];
                if (t.Type != TokenType.Word) continue;
                if (IsLanguageKeyword(t.Value)) continue;
                if (j + 1 < sig.Count && sig[j + 1].Value == "(")
                {
                    bool isMember = j > 0 && (sig[j - 1].Value == "." || sig[j - 1].Value == "::");
                    var cat = isMember ? HighlightCategory.Method : HighlightCategory.MethodGlobal;
                    results.Add(new DetectedSpan(t.SourceStart, t.Value.Length, cat));
                }
            }

            // ---- Pass 3: variables (scope-aware) ----
            // The buffer is a single method (or classDeclaration), so its whole scope is
            // visible: the signature gives parameters, declarations give locals. Any other
            // referenced identifier is non-local ("global"). Parameters -> italic, non-local
            // -> bold, locals -> unstyled (no span emitted).
            var paramNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var localNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            CollectScope(sig, paramNames, localNames);

            for (int j = 0; j < sig.Count; j++)
            {
                if (covered.Contains(j)) continue;            // already a Type/Macro
                var t = sig[j];
                if (t.Type != TokenType.Word) continue;
                if (IsLanguageKeyword(t.Value)) continue;
                if (t.Value.Length > 0 && t.Value[0] == '#') continue;                 // macro ref
                if (j + 1 < sig.Count && sig[j + 1].Value == "(") continue;            // call
                if (j > 0 && (sig[j - 1].Value == "." || sig[j - 1].Value == "::")) continue; // member

                if (paramNames.Contains(t.Value))
                    results.Add(new DetectedSpan(t.SourceStart, t.Value.Length, HighlightCategory.Parameter));
                else if (localNames.Contains(t.Value))
                    { /* local variable: leave unstyled */ }
                else
                    results.Add(new DetectedSpan(t.SourceStart, t.Value.Length, HighlightCategory.GlobalVar));
            }

            return results;
        }

        // Builds the method's scope: parameter names (from the signature) and local names
        // (from declaration statements). Buffer == one method, so this is the full scope.
        private static void CollectScope(List<Token> sig, HashSet<string> paramNames, HashSet<string> localNames)
        {
            // First '{' separates the signature from the body.
            int braceIdx = -1;
            for (int i = 0; i < sig.Count; i++)
                if (sig[i].Value == "{") { braceIdx = i; break; }

            // Parameters: the '(...)' immediately before the first '{' is the signature list.
            if (braceIdx > 0)
            {
                int closeParen = -1;
                for (int i = braceIdx - 1; i >= 0; i--)
                    if (sig[i].Value == ")") { closeParen = i; break; }

                if (closeParen > 0)
                {
                    int depth = 0, openParen = -1;
                    for (int i = closeParen; i >= 0; i--)
                    {
                        if (sig[i].Value == ")") depth++;
                        else if (sig[i].Value == "(") { depth--; if (depth == 0) { openParen = i; break; } }
                    }
                    if (openParen >= 0)
                    {
                        // A parameter name is a Word directly followed by ',' ')' or '='.
                        for (int i = openParen + 1; i < closeParen; i++)
                            if (sig[i].Type == TokenType.Word)
                            {
                                string nx = sig[i + 1].Value;
                                if (nx == "," || nx == ")" || nx == "=")
                                    paramNames.Add(sig[i].Value);
                            }
                    }
                }
            }

            // Locals: declaration statements in the body: <type>[.<w>]* <name> (,<name>)* [;|=]
            int start = (braceIdx >= 0) ? braceIdx + 1 : 0;
            int k = start;
            while (k < sig.Count)
            {
                bool typeStart = sig[k].Type == TokenType.Word &&
                                 (IsPrimitive(sig[k].Value) || !IsLanguageKeyword(sig[k].Value));
                if (typeStart)
                {
                    int c = CollectChain(sig, k);   // consume dotted type chain
                    if (c + 1 < sig.Count && sig[c + 1].Type == TokenType.Word && !IsLanguageKeyword(sig[c + 1].Value))
                    {
                        var names = new List<string> { sig[c + 1].Value };
                        int p = c + 2;
                        bool isDecl = false;
                        while (p < sig.Count)
                        {
                            string v = sig[p].Value;
                            if (v == ";" || v == "=") { isDecl = true; break; }
                            if (v == "," && p + 1 < sig.Count && sig[p + 1].Type == TokenType.Word)
                            { names.Add(sig[p + 1].Value); p += 2; continue; }
                            break; // anything else -> not a plain declaration
                        }
                        if (isDecl)
                        {
                            foreach (var nm in names) localNames.Add(nm);
                            int semi = p;
                            while (semi < sig.Count && sig[semi].Value != ";") semi++;
                            k = (semi < sig.Count) ? semi + 1 : sig.Count;
                            continue;
                        }
                    }
                }
                k++;
            }
        }

        // Returns the sig index of the last token in the dotted chain starting at start.
        // Chain = Word (. Word)* — dots must be Punctuation "."
        private static int CollectChain(List<Token> sig, int start)
        {
            int i = start;
            while (i + 2 < sig.Count &&
                   sig[i + 1].Type == TokenType.Punctuation && sig[i + 1].Value == "." &&
                   sig[i + 2].Type == TokenType.Word)
                i += 2;
            return i;
        }

        private static readonly HashSet<string> Primitives =
            new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "int", "int64", "str", "real", "boolean", "container",
                "date", "utcdatetime", "guid", "void", "anytype", "common"
            };

        private static bool IsPrimitive(string word) => Primitives.Contains(word);

        private static readonly HashSet<string> LanguageKeywords =
            new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "if", "else", "while", "for", "do", "switch", "case", "default",
                "return", "break", "continue", "new", "throw", "try", "catch", "finally",
                "true", "false", "null", "this", "super",
                "static", "public", "private", "protected", "internal", "abstract",
                "final", "extends", "implements", "class", "interface",
                "select", "from", "where", "join", "outer", "exists", "notexists",
                "order", "group", "by", "asc", "desc",
                "insert_recordset", "update_recordset", "delete_from",
                "ttsBegin", "ttsCommit", "ttsAbort",
                "firstOnly", "firstOnly10", "firstOnly100", "firstOnly1000",
                "firstFast", "forUpdate", "crossCompany", "validTimeState", "noFetch",
                "void", "int", "int64", "str", "real", "boolean", "container",
                "date", "utcdatetime", "guid", "anytype", "common",
                "like", "in", "is", "sum", "avg", "minof", "maxof", "count",
                "next", "pause", "print", "window", "sleep", "halt", "retry",
                "using", "namespace", "eventhandler", "server", "client",
                "display", "edit", "at", "as"
            };

        private static bool IsLanguageKeyword(string word) => LanguageKeywords.Contains(word);
    }
}
