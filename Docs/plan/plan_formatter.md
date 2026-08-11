# Plan — X++ Formatter MEF Extension

Port the XppForge token-based formatter to a self-contained C# `.dll`, bound to
`Ctrl+Shift+F` in the AX2012 editor, then extend it with the fixes and features
below. Same MEF/deploy architecture as the Rename extension.

## Status (2026-08-06)
- **S1–S5 DONE.** Core built and tested; net48 MEF DLL compiles; `publish\` populated.
- Validated against the `sampleMethod` sample + edge cases (macros, ternary, negative
  literals, array index, `while select` block, call chains): all correct, all idempotent,
  safety no-op on unbalanced input, string-brace safe. Every locked spec point holds.
- **S6 (runtime test in AX) — pending, user-driven.** Close AX → `Install-Local.bat` → reopen.
- **S7 (README/plan update + commit) — pending.** `NOTICE.md` written.
- Files: `JAEEFormatExtension\` — Token.cs, XppLexer.cs, XppFormatter.cs, FormatException.cs,
  FormatKeyProcessorProvider.cs, FormatKeyProcessor.cs, Properties\AssemblyInfo.cs,
  JAEEFormatExtension.csproj, NOTICE.md. Registered in the .sln; added to Install-Local.bat.

---

## 1. Locked decisions

- **Hotkey:** `Ctrl+Shift+F` (verified free in AX2012 editor).
- **Scope v1:** whole-buffer format (Format Document). Selection/range format later.
- **Assembly:** `JAEE.AX.EditorExtensions.Format.dll`
- **Project folder:** `JAEE.AX.EditorExtensions\JAEEFormatExtension\`
- **License:** clean-room reimplementation from the *algorithm* (XppForge ships no
  license). Do **not** copy their JS. Add `NOTICE` crediting the approach.
- **Safety guard:** keep `VerifyTokenSafety` — if the re-tokenized output differs from
  input (ignoring whitespace; X++ words case-insensitive), **abort and return original
  unchanged**. Worst case = no-op, never corruption. All rules below operate *within*
  this guarantee: they only move whitespace/newlines, never add/remove tokens.
  (Consequence: auto-adding braces is impossible — it would change the token stream and
  trip the guard. Braceless bodies are reflowed, not braced.)

---

## 2. Formatting spec

Derived from testing XppForge on real code + confirmed decisions.

### Indentation & braces
- Allman braces. Indent = **4 spaces** × brace depth. **Spaces only, never tabs**
  (tabs break column alignment at different tab widths).
- `{` → own line at current depth, then depth++.
- `}` → depth--, own line.

### Spacing
- Binary operators surrounded by single space (`==`, `&&`, `>`, `=`, …).
- **No trailing space for prefix-unary** `!` `~` `++` `--`, and unary `+`/`-`
  (when preceded by an operator, `(`, `,`, `return`, `;`, or line start).
  → fixes `if (! dbLog.RecId)` becoming `if (!dbLog.RecId)`.
- `.` and `::` — no surrounding space.
- `,` → `, `.
- Keyword before `(` gets a space: `if/for/while/switch/catch` and also `return`/`throw`
  → `return (x)`, `if (…)`. A plain identifier before `(` is a call → no space (`foo(x)`,
  `enumNum(X)`). Only the control keywords drive braceless-body indentation, not
  `return`/`throw`.

### switch / case
- `case X:` / `default:` labels sit at the switch brace level; the colon is tight
  (`case 0:`, not `case 0 :`).
- Case **bodies** indent one level deeper than the label; the first body statement is
  forced onto its own line (never left trailing on the `case` line).
- Fall-through labels (`case A:` `case B:`) and `default:` handled; enum labels like
  `case Type::Member:` work (the `::` is a separate token from the label `:`).
- One blank line is inserted before each `case`/`default` label except the first in a
  switch (this is the one place the formatter *inserts* a blank line rather than only
  preserving existing ones).

### Comments
- Own-line comments are re-indented to the surrounding code level.
- Line comments are normalized to the slash run + exactly one space + trimmed text
  (`//        x` → `// x`, `///  x` → `/// x`); block comments `/* … */` are untouched.
  This is the one place comment *content* is altered (leading whitespace only); the
  safety verifier compares comments with whitespace collapsed so the change is allowed.

### Strings
- X++ strings use **both** single and double quotes (`'text'` and `"text"`). Both are
  consumed whole by the lexer and never reformatted. (Critical: single-quoted strings
  were originally missed, which let the formatter rewrite string *contents* like
  `'%1'`→`'% 1'`; the safety net can't catch that because the mis-tokenization makes the
  streams compare equal.)

### Argument-list line breaks — "chop when multiline"
- A parenthesized argument list that spans more than one source line is broken after
  **every top-level comma** (one argument per line, continuation indent = statement
  indent + one level per enclosing multiline list); the closing `)` follows the last
  argument. An all-on-one-line list is reflowed to one line. Nested lists are evaluated
  independently. Mirrors the ReSharper/Prettier "chop when multiline" rule (stock VS
  mostly just preserves existing breaks).

### Newline convention
- Output preserves the source's newline style (emit CRLF if the buffer is CRLF) and its
  trailing-newline state, so a whole-buffer replace does not flip every line ending.

### Statement / line structure
- `;` terminates a statement → newline — **but only at paren-depth 0**.
  A `;` inside `()` is a separator (→ single space).
  → fixes the `for (a; b; c)` header being split across 3 lines.
- **Braceless control body:** after a control header `)` (or `else`/`do`) whose next
  significant token is not `{`, put the single body statement on its **own line,
  indented one level**; return to normal indent after that statement's `;`.
  → fixes `if (sc == "I") mlogtype = …;` collapsing onto one line.
- **Comments:**
  - A comment that was on the **same source line as preceding code** stays **trailing**
    on that code line. → fixes `// Insert Update Delete` being torn onto its own line.
  - A comment that was on its **own source line** stays on its own line at current indent.
  - Comment **contents** are never altered.
- **Blank lines:** preserve a single blank line wherever the author had ≥1; collapse
  runs of 2+ to exactly one. (Requires the tokenizer to record newline counts in space
  tokens.)

### X++ queries (`select` / `while select`) — SQL-style re-layout
Actively re-laid-out (not just preserved). `while` on its own line; select block indented
under it; `select` modifiers+fields on the select line; `from`/`join`/`where` operands
aligned to the **field column** (where the select fields begin); blank line before each
`join`; `where` conditions with `&&`/`||` right-aligned and comparison operators aligned.
See `JAEEFormatExtension/README.md` for the worked example. Implemented in `Renderer`
(`TryRenderSelect`/`RenderWhere`); operands rendered via an inline sub-`Renderer`. Falls
back to normal rendering if the statement is malformed. Whitespace-only → safety-checked
and idempotent.

### Keyword casing (canonicalize before formatting)
Whole-word, case-insensitive replace: `ttsbegin→ttsBegin, ttscommit→ttsCommit,
ttsabort→ttsAbort, firstonly[/10/100/1000]→firstOnly…, firstfast→firstFast,
forupdate→forUpdate, crosscompany→crossCompany, validtimestate→validTimeState,
notexists→notExists, optimisticlock→optimisticLock, pessimisticlock→pessimisticLock,
repeatableread→repeatableRead, generateonly→generateOnly,
forcenestedloop→forceNestedLoop, forceplaceholders→forcePlaceholders`. Extend as needed.

### Column alignment — **always re-align** (Phase B post-pass, spaces only)
Operate on the rendered lines. A **group** is a maximal run of same-kind, same-indent
lines. **Comment-only lines and blank lines are transparent** — they do not reset the
column — so a whole contiguous declaration/assignment block aligns to a single column
(widest type across the block). Only a non-matching *code* line or an indent change ends
a group. (`BlanksBreakAlignment = false`; set true to make blank lines separate
independent sub-groups.)

- **Declaration group** — consecutive lines matching
  `<indent> <type> <name> [= <rhs>] ; [// comment]`
  (type = word, optionally `<…>` / `[]`; single statement; no control keyword/brace).
  Align, within the group, these columns to their max width + 1 space:
  **name**, **`=`**, and the trailing **`//` comment**.
  Lines without `= rhs` still align their trailing comment to the group's comment column.
- **Assignment group** — consecutive lines matching
  `<indent> <lhs> = <rhs> ; [// comment]` (lhs = dotted/indexed identifier; single
  statement). Align the **`=`** column and the trailing **`//` comment** column.
- Lines that don't cleanly match are passed through untouched and break the group.
- **Idempotent:** deterministic max-width padding → second run == first run. This is a
  hard test requirement (S6).

Example (target):
```
DatabaseLog     dbLog;
TableId         mtableId = tableNum(DirPartyLocation);
FieldId         mfieldid = fieldNum(DirPartyLocation, IsPrimary);
str             logtypes = "IUD";                // Insert Update Delete
```

---

## 3. Architecture — two phases

**Phase A — token render (newline-aware).** Port of XppForge `FormatDelimited`, but the
tokenizer records, for each `Space` token, whether it contains a newline and how many.
The renderer uses that to: keep trailing comments, preserve `select` line breaks, and
emit single blank lines. Produces indented lines (no alignment yet).

**Phase B — alignment post-pass.** Line-based. Classify each line (declaration /
assignment / other), group maximal runs, compute per-cell max widths, re-pad with spaces.

Both phases are VS-independent and unit-testable without AX.

---

## 4. Components (namespace `JAEE.AX.EditorExtensions.Format`)

- `Token.cs` — `enum TokenType { Space, Comment, String, Number, Word, Operator,
  Punctuation, Directive }`; `struct Token { Type, Value, Line, Column, NewlinesBefore }`.
- `XppLexer.cs` — `Tokenize`: whitespace (record newline count), `/* */`, `//` line
  comments (flag whether same-line-as-code via `NewlinesBefore==0`), strings, numbers,
  words, longest-match operators, single punctuation, **and X++ `#`-directives**
  (`#define/#localmacro/#globalmacro/#endmacro/#if/#endif/#macrolib/#undef` at line start
  → read to EOL as `Directive`). Operator table:
  `=== !== >>> <<= >>= => ??= ?. ** == != <= >= && || ++ -- += -= *= /= %= :: -> .. << >> ?? ?:`
  then single-char.
- `XppFormatter.cs`:
  - `Canonicalize` (keyword casing)
  - `BalancedTokens` (bracket check; abort on unbalanced)
  - `RenderTokens` (Phase A state machine: brace depth, paren depth, braceless-body
    hanging indent, trailing-comment handling, select-span line-break preservation,
    blank-line emission)
  - `AlignColumns` (Phase B: classify → group → pad)
  - `Significant`, `Comparable`, `VerifyTokenSafety`
  - public `static string Format(string source)` = Canonicalize → tokenize →
    BalancedTokens → RenderTokens → AlignColumns → VerifyTokenSafety
- `FormatException.cs` — typed error w/ line/column; `Format` catches → returns original.

Phase-A state machine, per significant token:
- track `parenDepth` (`(`/`)`), `braceDepth` (indent).
- `;`: if `parenDepth>0` → space; else → terminator newline (and close any pending
  braceless-body indent).
- `(` after control word → mark "control header"; on its matching `)` peek next token;
  if not `{` → set pending hanging indent (+1 for the next statement).
- `select`/`while select` word at `parenDepth 0` → enter select-span until matching `;`;
  inside, honor original newlines (`Space.NewlinesBefore>0` → newline + contIndent).

---

## 5. Actionable steps

### S1 — Project skeleton
- New `JAEE.AX.EditorExtensions\JAEEFormatExtension\`; copy Rename's `.csproj` →
  `JAEEFormatExtension.csproj` (`AssemblyName=…Format`, new GUID, net48,
  `RestoreProjectStyle=PackageReference`, `ReferenceAssemblies.net48` 1.0.3, the 6 VS
  HintPaths `Private=False`, WPF/MEF refs). Copy/edit `Properties\AssemblyInfo.cs`.
  `Directory.Build.targets` auto-copies to `publish\`.

### S2 — Formatter core (VS-independent) — **the bulk of the work**
- `Token.cs`, `XppLexer.cs` (with newline tracking + `#`-directives).
- `XppFormatter.cs` Phase A: `Canonicalize`, `BalancedTokens`, `RenderTokens`
  (braces/indent/spacing, `;`-paren guard, braceless-body break, trailing vs standalone
  comments, blank-line collapse, select line-break preservation, unary `! ~ ++ -- +/-`).
- `XppFormatter.cs` Phase B: `AlignColumns` (decl + assignment groups; name/`=`/comment).
- `VerifyTokenSafety` + `Format` entry.
- Build as a plain library first; iterate against the test corpus (S2t) before wiring MEF.

### S2t — Test corpus (no VS needed)
- Drop the `sampleMethod` sample + edge cases into a tiny console/xUnit harness.
- Assert: for-header intact, trailing comments kept, braceless if broken to own line,
  `!` no-space, blank lines single, select line breaks preserved+indented, decl &
  assignment columns aligned, **idempotency (format(format(x))==format(x))**, and
  safety-abort on deliberately unbalanced input.

### S3 — MEF wiring (mirror Rename)
- `FormatKeyProcessorProvider.cs`: `[Export(IKeyProcessorProvider)] [Name(...)]
  [ContentType("text")] [TextViewRole(Document)] [Order(Before="DefaultKeyProcessor")]`
  → `new FormatKeyProcessor(view)`.
- `FormatKeyProcessor.cs : KeyProcessor`:
  - `KeyDown`: `Key==F && Modifiers==(Control|Shift)` → `args.Handled=true`;
    `Dispatcher.BeginInvoke(Normal, TryFormat)` (deferred — same pipeline fix as Rename).
  - `TryFormat`: capture caret offset → `text=snapshot.GetText()` →
    `formatted=XppFormatter.Format(text)` → if unchanged return (no undo entry) → one
    `ITextEdit`: `Replace(0, len, formatted); Apply()` (atomic single Ctrl+Z) → restore
    caret near offset → `_view.VisualElement.Focus()`. try/catch → on failure, no-op.

### S4 — Solution + deploy
- Add project to `JAEE.AX.EditorExtensions\JAEE.AX.EditorExtensions.sln`.
- Add to `Install-Local.bat`:
  `copy /Y "%Publish%\JAEE.AX.EditorExtensions.Format.dll" "%EditorComponents%\"`

### S5 — Build (Release)
- Confirm `publish\JAEE.AX.EditorExtensions.Format.dll`. Add any missing `System.*` refs
  explicitly (HighlightWord precedent needed `System.Drawing`).

### S6 — Runtime test in AX (user-driven)
- **Close AX** → run `Install-Local.bat` → reopen (one bad DLL poisons the MEF catalog).
- Re-run the `sampleMethod` sample in-editor; verify every S2t assertion holds live,
  plus: single `Ctrl+Z` reverts the whole format; `Ctrl+Shift+F` no collision;
  macro-heavy method keeps `#`-directives on own lines; unbalanced braces → silent no-op.

### S7 — Docs & commit
- `NOTICE` (approach credit), update `README.md` + `plan.md`, commit.

---

## 6. Known limitations (document in v1)
- Whole-buffer only; no range/selection format yet.
- Comment **contents** never reflowed (safety).
- `select` is line-break-preserving, not reflowing — malformed/very-wide queries stay wide.
- Alignment only fires on clean single-statement decl/assignment lines; anything with a
  control keyword, brace, or multiple statements is passed through.
- Canonicalization list is finite; unknown keywords keep author casing.
- Full-buffer replace loses exact caret position — mitigated by offset restore, not perfect.

## 7. Effort note
Bigger than the initial "simple port": Phase A state machine + Phase B alignment ≈
**800–1200 lines** of C#. The alignment pass and the select line-break preservation are
the two hardest pieces; build and test them against the corpus (S2t) before MEF wiring.
