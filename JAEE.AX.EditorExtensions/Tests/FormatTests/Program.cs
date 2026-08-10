using System;
using System.Collections.Generic;
using JAEE.AX.EditorExtensions;
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

            Case("multiline container literal chops, not collapses",
                "void f()\n{\n    container c = [1,\n2,\n3];\n}",
                ("not collapsed", o => o.Contains("[1,\n")),
                ("= [ keeps its space", o => o.Contains("= [")),
                ("no =[ ", o => !o.Contains("=[")));

            Case("one-line container stays; indexing stays tight",
                "void f(){ container c = [1, 2, 3]; x = arr[i]; }",
                ("[1, 2, 3] one line", o => o.Contains("[1, 2, 3]")),
                ("arr[i] tight", o => o.Contains("arr[i]")));

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

            // dotted types align; very different widths split into separate columns
            Case("dotted types recognized and width-split",
                "void f()\n{\n    int i;\n    str text;\n" +
                "    System.Windows.Controls.TabControl tc = h.control();\n" +
                "    System.Windows.Controls.TabItem tab;\n}",
                ("short types share a column", o =>
                    ColOfSecondWord(o, "int") == ColOfSecondWord(o, "str")),
                ("dotted types share a column", o =>
                    ColOfSecondWord(o, "System.Windows.Controls.TabControl") ==
                    ColOfSecondWord(o, "System.Windows.Controls.TabItem")),
                ("short and long are NOT the same column", o =>
                    ColOfSecondWord(o, "int") != ColOfSecondWord(o, "System.Windows.Controls.TabControl")));

            Case("moderately different widths stay together",
                "void f()\n{\n    int i;\n    SalesTable st;\n    boolean ok;\n}",
                ("int and SalesTable share a column", o =>
                    ColOfSecondWord(o, "int") == ColOfSecondWord(o, "SalesTable")));

            // --- ternary chop (? and : aligned under the =) ---
            Case("multiline ternary chops, ? and : aligned under =",
                "void f()\n{\n    context = cond ? trueBranch :\n            falseBranch;\n}",
                ("condition stays on the = line", o => o.Contains("context = cond\n")),
                ("? aligned under = (col 12)", o => o.Contains("\n            ? trueBranch")),
                (": aligned under = (col 12)", o => o.Contains("\n            : falseBranch")));

            Case("one-line ternary stays inline",
                "void f(){ x = a ? b : c; }",
                ("a ? b : c one line", o => o.Contains("x = a ? b : c;")));

            // --- blank line after a declaration block ---
            Case("blank inserted after declarations before code",
                "void f()\n{\n    int i;\n    str text;\n    doWork(i);\n}",
                ("blank after decls", o => o.Contains("str text;\n\n    doWork")));

            Case("blank inserted after a lone ';' separator",
                "void f()\n{\n    int i;\n    ;\n    doWork(i);\n}",
                ("blank after the ;", o => o.Contains("    ;\n\n    doWork")));

            Case("existing blank after declarations is not doubled",
                "void f()\n{\n    int i;\n\n    doWork(i);\n}",
                ("still a single blank", o => !o.Contains("int i;\n\n\n")));

            // --- safety ---
            Case("unbalanced input is a no-op",
                "void f(){ if (a { b(); }",
                ("returns original unchanged", o => o == "void f(){ if (a { b(); }"));

            // --- newline convention ---
            Case("CRLF preserved",
                "void f()\r\n{\r\nint x=1;\r\n}\r\n",
                ("no lone LF", o => !System.Text.RegularExpressions.Regex.IsMatch(o, "(?<!\r)\n")));

            // --- multiline boolean condition: chop + aligned compare column ---
            Case("multiline if condition chops and aligns compare column",
                "void f()\n{\n    if (alpha() != 0\n|| b() != 0)\n    {\n        x();\n    }\n}",
                ("first operand stays on the if( line", o => o.Contains("if (alpha() != 0")),
                ("|| leads the continuation, operand aligned, != aligned",
                    o => o.Contains("    ||  b()     != 0)")));

            Case("multiline condition — user's dotted-call example aligns",
                "void f()\n{\n    if (purchLine.receivedInTotal() != 0\n|| purchLine.registered() != 0\n|| purchLine.invoicedInTotal() != 0)\n    {\n        x();\n    }\n}",
                ("row 0 on if( line", o => o.Contains("if (purchLine.receivedInTotal() != 0")),
                ("row 1 aligned under row 0's !=", o => o.Contains("    ||  purchLine.registered()      != 0")),
                ("row 2 aligned, close paren attached", o => o.Contains("    ||  purchLine.invoicedInTotal() != 0)")));

            Case("one-line condition stays on one line",
                "void f()\n{\n    if (a() != 0 || b() != 0)\n    {\n        x();\n    }\n}",
                ("not chopped", o => o.Contains("if (a() != 0 || b() != 0)")));

            Case("multiline condition without compare op chops with plain single space",
                "void f()\n{\n    if (isA()\n|| isB())\n    {\n        x();\n    }\n}",
                ("chopped, no column", o => o.Contains("if (isA()")),
                ("|| continuation single space", o => o.Contains("    || isB())")));

            Case("one-line condition keeps a space between && and a nested paren",
                "void f()\n{\n    if (a == 1 && (b == 2 || c == 3))\n    {\n        x();\n    }\n}",
                ("&& ( spaced", o => o.Contains("a == 1 && (b == 2 || c == 3)")),
                ("never glued &&(", o => !o.Contains("&&(")));

            Case("multiline mixed condition (compare row + paren group) plain-chops",
                "void f()\n{\n    if (a == 1\n&& (b == 2 || c == 3))\n    {\n        x();\n    }\n}",
                ("row 0 on if( line", o => o.Contains("if (a == 1")),
                ("&& leads with a single space, no stray padding",
                    o => o.Contains("    && (b == 2 || c == 3))")));

            // ---- Classifier tests ----

            ClassifierCase("macro: directive token",
                "#define.MyMacro(10)",
                spans => HasCategory(spans, HighlightCategory.Macro));

            ClassifierCase("macro: inline #ref",
                "void f(){ x = #MyMacro; }",
                spans => HasCategory(spans, HighlightCategory.Macro));

            ClassifierCase("primitive type colored",
                "void f(){ int x; }",
                spans => HasSpan(spans, "int", "void f(){ int x; }", HighlightCategory.Type));

            ClassifierCase("type before ::",
                "void f(){ boolean b = NoYes::Yes; }",
                spans => HasSpan(spans, "NoYes", "void f(){ boolean b = NoYes::Yes; }", HighlightCategory.Type));

            ClassifierCase("type after new",
                "void f(){ System.IO.MemoryStream ms = new System.IO.MemoryStream(); }",
                spans => HasSpan(spans, "System.IO.MemoryStream", "void f(){ System.IO.MemoryStream ms = new System.IO.MemoryStream(); }", HighlightCategory.Type));

            ClassifierCase("global function colored as MethodGlobal",
                "void f(){ info(\"hello\"); }",
                spans => HasSpan(spans, "info", "void f(){ info(\"hello\"); }", HighlightCategory.MethodGlobal));

            ClassifierCase("global function is not the instance Method category",
                "void f(){ info(\"hello\"); }",
                spans => !HasSpan(spans, "info", "void f(){ info(\"hello\"); }", HighlightCategory.Method));

            ClassifierCase("instance method colored as Method (not global)",
                "void f(){ _bitmap.MakeTransparent(); }",
                spans => HasSpan(spans, "MakeTransparent", "void f(){ _bitmap.MakeTransparent(); }", HighlightCategory.Method));

            ClassifierCase("static :: method colored as Method (not global)",
                "void f(){ x = File::GetFileFromUser(); }",
                spans => HasSpan(spans, "GetFileFromUser", "void f(){ x = File::GetFileFromUser(); }", HighlightCategory.Method));

            ClassifierCase("nested global call inside args stays global",
                "void f(){ info(strFmt(\"%1\", conPeek(record, 1))); }",
                spans => HasSpan(spans, "strFmt", "void f(){ info(strFmt(\"%1\", conPeek(record, 1))); }", HighlightCategory.MethodGlobal)
                      && HasSpan(spans, "conPeek", "void f(){ info(strFmt(\"%1\", conPeek(record, 1))); }", HighlightCategory.MethodGlobal));

            ClassifierCase("new MemoryStream stays Type, not also Method",
                "void f(){ System.IO.MemoryStream ms = new System.IO.MemoryStream(); }",
                spans => !HasSpan(spans, "MemoryStream", "void f(){ System.IO.MemoryStream ms = new System.IO.MemoryStream(); }", HighlightCategory.Method));

            ClassifierCase("if not colored as method",
                "void f(){ if (a) { } }",
                spans => !HasSpan(spans, "if", "void f(){ if (a) { } }", HighlightCategory.Method));

            ClassifierCase("type after 'is'",
                "void f(){ if (x is EcoResProduct) {} }",
                spans => HasSpan(spans, "EcoResProduct", "void f(){ if (x is EcoResProduct) {} }", HighlightCategory.Type));

            ClassifierCase("type after 'is' with chained receiver",
                "void f(){ if (element.args().record() is EcoResProduct) {} }",
                spans => HasSpan(spans, "EcoResProduct", "void f(){ if (element.args().record() is EcoResProduct) {} }", HighlightCategory.Type));

            ClassifierCase("type after 'as'",
                "void f(){ test2 = test as AccDistConstPayroll; }",
                spans => HasSpan(spans, "AccDistConstPayroll", "void f(){ test2 = test as AccDistConstPayroll; }", HighlightCategory.Type));

            ClassifierCase("'as' keyword itself not colored as type",
                "void f(){ test2 = test as AccDistConstPayroll; }",
                spans => !HasSpan(spans, "as", "void f(){ test2 = test as AccDistConstPayroll; }", HighlightCategory.Type));

            // ---- Scope-aware variable tests (buffer == one method) ----

            ClassifierCase("parameter variable is Parameter (muted)",
                "void main(Args _args){ _args.parmEnum(); }",
                spans => HasSpan(spans, "_args", "void main(Args _args){ _args.parmEnum(); }", HighlightCategory.Parameter));

            ClassifierCase("local variable is Local (muted)",
                "void f(){ container record; record = conNull(); }",
                spans => HasSpan(spans, "record", "void f(){ container record; record = conNull(); }", HighlightCategory.Local));

            ClassifierCase("non-local variable is left plain (no span)",
                "void f(){ custTable.AccountNum = '1'; }",
                spans => !HasSpan(spans, "custTable", "void f(){ custTable.AccountNum = '1'; }", HighlightCategory.Local)
                      && !HasSpan(spans, "custTable", "void f(){ custTable.AccountNum = '1'; }", HighlightCategory.Parameter));

            ClassifierCase("member after '.' is not a variable",
                "void f(){ int x; x = obj.Field; }",
                spans => !HasSpan(spans, "Field", "void f(){ int x; x = obj.Field; }", HighlightCategory.Local));

            // Full screenshot method: file/record are locals (muted), info/strFmt/conPeek
            // are global functions, AsciiStreamIo is a type.
            {
                string src = "public static void main(Args _args){ AsciiStreamIo file; container record; "
                           + "file = AsciiStreamIo::constructForRead(x); record = file.read(); "
                           + "info(strFmt(\"%1\", conPeek(record, 1))); }";
                ClassifierCase("screenshot: local 'file' is Local",
                    src, spans => HasSpan(spans, "file", src, HighlightCategory.Local));
                ClassifierCase("screenshot: local 'record' is Local",
                    src, spans => HasSpan(spans, "record", src, HighlightCategory.Local));
                ClassifierCase("screenshot: global 'info' is MethodGlobal",
                    src, spans => HasSpan(spans, "info", src, HighlightCategory.MethodGlobal));
                ClassifierCase("screenshot: type 'AsciiStreamIo' is Type",
                    src, spans => HasSpan(spans, "AsciiStreamIo", src, HighlightCategory.Type));
            }

            Console.WriteLine();
            Console.WriteLine(_failures == 0 ? "ALL TESTS PASSED" : $"{_failures} ASSERTION(S) FAILED");
            return _failures;
        }

        private static void ClassifierCase(string name, string input, Func<List<DetectedSpan>, bool> check)
        {
            var spans = XppSyntaxDetector.Classify(input);
            Assert(name, "classifier check", check(spans));
        }

        private static bool HasCategory(List<DetectedSpan> spans, HighlightCategory cat)
        {
            foreach (var s in spans) if (s.Category == cat) return true;
            return false;
        }

        private static bool HasSpan(List<DetectedSpan> spans, string text, string source, HighlightCategory cat)
        {
            int idx = source.IndexOf(text, StringComparison.Ordinal);
            if (idx < 0) return false;
            foreach (var s in spans)
                if (s.Category == cat && s.Start == idx && s.Length == text.Length) return true;
            return false;
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
