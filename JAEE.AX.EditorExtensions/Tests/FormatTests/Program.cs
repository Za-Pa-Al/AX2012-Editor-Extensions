using System;
using JAEE.AX.EditorExtensions.Format;

namespace JAEE.AX.EditorExtensions.Format.Tests
{
    // Console regression harness for the X++ formatter core. Run: dotnet run --project Tests/FormatTests
    // Exit code = number of failed assertions.
    internal static class Program
    {
        private static int _failures;

        private static int Main()
        {
            // --- string safety (single quotes were once corrupted) ---
            Case("single-quote strings preserved",
                "void f(){ str s = ''; info(strFmt('a%1b', 1)); s = '0'; }",
                ("empty '' kept", o => o.Contains("''")),
                ("'a%1b' verbatim", o => o.Contains("'a%1b'")),
                ("'0' kept", o => o.Contains("'0'")),
                ("no space inserted in %1", o => !o.Contains("% 1")));

            Case("double-quote strings preserved",
                "void f(){ str p = \"c:\\temp\\\"; info(\"a { b ; c\"); }",
                ("brace/semicolon in string harmless", o => o.Contains("\"a { b ; c\"")));

            // --- spacing ---
            Case("for header stays on one line",
                "void f(){ for(i=0;i<n;i++){ x++; } }",
                ("for spaced, one line", o => o.Contains("for (i = 0; i < n; i++)")));

            Case("return keeps space before paren",
                "boolean f(){ return(a==b); }",
                ("return (…)", o => o.Contains("return (a == b)")),
                ("not return(", o => !o.Contains("return(")));

            Case("call stays tight",
                "void f(){ x = enumNum(Foo); }",
                ("enumNum(Foo)", o => o.Contains("enumNum(Foo)")));

            Case("unary bang tight",
                "void f(){ if (!a.RecId){ b(); } }",
                ("!a", o => o.Contains("(!a.RecId)")));

            // --- braceless control body ---
            Case("braceless if body on own line",
                "void f(){ if(a) b(); }",
                ("if on its own line", o => o.Contains("    if (a)")),
                ("body indented", o => o.Contains("        b();")));

            // --- switch / case ---
            Case("switch case indentation + tight colon + blank between cases",
                "void f(){ switch(x){ case 0 : a(); break; case 1 : b(); break; } }",
                ("case 0 at switch+1", o => o.Contains("        case 0:")),
                ("body at switch+2", o => o.Contains("            a();")),
                ("tight colon", o => !o.Contains("case 0 :")),
                ("blank before 2nd case", o => o.Contains("\n\n        case 1:")));

            // --- comments ---
            Case("line comment normalized to one space",
                "void f(){ int x; //     hello\n }",
                ("// hello", o => o.Contains("// hello")),
                ("no //     hello", o => !o.Contains("//     hello")));

            // a comment with NO space after // must not abort the whole format
            Case("no-space comment gets a space (does not abort)",
                "void f()\n{\n    ttsBegin;\n//delete_from t where t.a == 1;\n    st.x = 1;\n    ttsCommit;\n}",
                ("//delete_from -> // delete_from", o => o.Contains("// delete_from")),
                ("format actually applied (indented body)", o => o.Contains("    ttsBegin;")),
                ("not left as raw //delete", o => !o.Contains("\n//delete_from")));

            // --- argument lists ---
            Case("multiline arg list chopped",
                "void f(){ x = strFmt('a',\n b,\n c); }",
                ("broken after commas", o => o.Contains("',\n") && o.Contains("b,\n")));

            Case("one-line arg list stays one line",
                "void f(){ foo(a, b, c); }",
                ("foo(a, b, c)", o => o.Contains("foo(a, b, c)")));

            // --- alignment (comment transparent, whole block) ---
            Case("declaration block aligned across a comment",
                "class C{\n    FormRadioControl a;\n    // note\n    FormReferenceControl b;\n}",
                ("names aligned to one column", o =>
                {
                    int ca = ColOfSecondWord(o, "FormRadioControl");
                    int cb = ColOfSecondWord(o, "FormReferenceControl");
                    return ca > 0 && ca == cb;
                }));

            // --- select statements (SQL-style layout) ---
            Case("while select multi-column layout",
                "void f(){ while select firstFast sum(x) from tabA join tabB where tabA.id == tabB.id && tabA.q > 0 { y += tabA.x; } }",
                ("while on its own line", o => o.Contains("while\n")),
                ("select fields collapsed", o => o.Contains("select firstFast sum(x)")),
                ("join present", o => o.Contains("join")),
                ("&& continuation", o => o.Contains("&& ")),
                ("string in where kept", o => o.Contains("tabA.q")));

            Case("plain select reformatted and stable",
                "void f(){ select firstOnly ctA where ctA.a == 1 && ctA.b == 'x'; }",
                ("select buffer on select line", o => o.Contains("select firstOnly ctA")),
                ("where present", o => o.Contains("where")),
                ("single-quote string kept", o => o.Contains("'x'")));

            // --- safety ---
            Case("unbalanced input is a no-op",
                "void f(){ if (a { b(); }",
                ("returns original unchanged", o => o == "void f(){ if (a { b(); }"));

            // --- newline convention ---
            Case("CRLF preserved",
                "void f()\r\n{\r\nint x=1;\r\n}\r\n",
                ("no lone LF", o => !System.Text.RegularExpressions.Regex.IsMatch(o, "(?<!\r)\n")));

            Console.WriteLine();
            Console.WriteLine(_failures == 0 ? "ALL TESTS PASSED" : $"{_failures} ASSERTION(S) FAILED");
            return _failures;
        }

        private static void Case(string name, string input, params (string label, Func<string, bool> check)[] checks)
        {
            string o;
            try { o = XppFormatter.Format(input); }
            catch (Exception e) { Assert(name, "did not throw (" + e.Message + ")", false); return; }

            Assert(name, "idempotent", XppFormatter.Format(o) == o);
            foreach (var c in checks) Assert(name, c.label, c.check(o));
        }

        private static void Assert(string name, string label, bool ok)
        {
            if (!ok) _failures++;
            Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name} — {label}");
        }

        // column (0-based) at which the word following `firstWord` starts, on the line
        // containing `firstWord`; -1 if not found. Used to check alignment.
        private static int ColOfSecondWord(string text, string firstWord)
        {
            foreach (string line in text.Split('\n'))
            {
                int fi = line.IndexOf(firstWord, StringComparison.Ordinal);
                if (fi < 0) continue;
                int i = fi + firstWord.Length;
                while (i < line.Length && line[i] == ' ') i++;
                return i;
            }
            return -1;
        }
    }
}
