# JAEE X++ Formatter (`JAEE.AX.EditorExtensions.Format`)

A VS-style **Format Document** for the AX2012 X++ code editor. Press **`Ctrl+Shift+F`**
in a code editor window; the current buffer is reformatted in a single, atomic edit
(one `Ctrl+Z` undoes it).

It is a MEF editor extension (an `IKeyProcessorProvider` dropped into
`…\Client\Bin\EditorComponents`), same mechanism as the other JAEE extensions.

---

## Safety model — it can only move whitespace

The formatter can **never change the meaning of your code**. It works like this:

```
tokenize → format (whitespace only) → re-tokenize the output
         → if the significant token stream changed, throw the result away and
           return the ORIGINAL text unchanged.
```

So the worst case is a **no-op**, never corruption. Comparison ignores whitespace and is
case-insensitive on words (so keyword-casing fixes are allowed) and whitespace-insensitive
inside comments (so comment tidy-ups are allowed). If the input has unbalanced brackets or
an unterminated string/comment, the format is skipped and the buffer is left as-is.

> Strings are consumed whole by the lexer — **both** single-quoted (`'…'`) and
> double-quoted (`"…"`), because X++ uses both — so nothing inside a string or comment is
> ever reformatted.

---

## What it does

### Indentation & braces
- 4 spaces per level. **Allman** braces (`{` and `}` on their own lines).
- Output preserves the source's newline convention (CRLF stays CRLF) and trailing-newline.

### Spacing
- Binary operators get single spaces (`a == b`, `x && y`).
- Prefix/postfix unary tight: `!x`, `~x`, `i++`, `-1`.
- `.` and `::` tight (`this.foo`, `Type::Member`).
- `,` → `, `.
- A **keyword** before `(` keeps a space — `if/for/while/switch/catch` and
  `return/throw` → `return (x)`. A plain call is tight → `foo(x)`, `enumNum(X)`.

### Statements
- `;` ends a statement (newline) — but a `;` inside `(…)` is a separator, so
  `for (i = 0; i < n; i++)` stays on one line.
- A braceless control body goes on its own indented line:
  `if (x)` ⏎ `    doThing();`.

### switch / case
- `case`/`default` labels sit one level under `switch`; their bodies one level deeper.
- `case 0:` — colon tight; first body statement never left on the `case` line.
- One blank line before each `case`/`default` except the first.
- Fall-through (`case A:` `case B:`) and enum labels (`case Type::Member:`) handled.

### Comments
- Own-line comments are re-indented to the surrounding code.
- Line comments normalized to slashes + one space + trimmed text (`//        x` → `// x`).
- Block comments `/* … */` are left untouched.

### Blank lines
- A single blank line is preserved; runs of 2+ collapse to one.

### `select` / `while select` statements (SQL-style layout)
Selects are actively re-laid-out into aligned columns:
- `while` goes on its own line; the `select` block is indented under it.
- The select modifiers + field list go on the `select` line; extra fields (comma-separated)
  wrap to their own lines aligned under the first field.
- `from` / `join` / `where` each start a line; their **operands align to the field column**
  (the column where the select fields begin).
- A blank line precedes each `join`.
- In a `where`, the first condition is on the `where` line; each `&&` / `||` continuation is
  on its own line with the operator **right-aligned** so conditions line up, and the
  **comparison operators** (`==`, `>`, …) are aligned into a column.

```
while
    select firstFast crossCompany sum(lineAmount)
    from                          orderLine

    join                          orderHeader
    where                         orderLine.orderId == orderHeader.orderId

    join                          customer
    where                         orderHeader.account == customer.accountNum
                               && customer.currency   == 'EUR'
                               && orderLine.shipDate  > systemDateGet()
```

### Argument lists — "chop when multiline"
- A parenthesized argument list that spans multiple source lines is broken after **every
  top-level comma** (one argument per line); a one-line list is reflowed to one line.
  Nested lists decide independently.

### Keyword casing
- Canonicalized: `ttsbegin`→`ttsBegin`, `forupdate`→`forUpdate`, `notexists`→`notExists`,
  `firstonly`→`firstOnly`, … (whole-word, case-insensitive).

### Column alignment (always re-aligned, spaces only)
- **Declarations** — a contiguous block of `Type name [= rhs];` lines is aligned to one
  column: the **name**, the **`=`**, and any trailing **`//` comment**.
- **Assignments** — a run of `lhs = rhs;` lines has its **`=`** and trailing comment aligned.
- Comment-only lines and blank lines are **transparent** to alignment (they don't reset
  the column), so a whole declaration block lines up even with comments/blanks between.
- Deterministic → **idempotent** (running the formatter twice gives the same result).

### Preprocessor
- `#define` / `#localmacro` / … directive lines are kept on their own line; inline macro
  references (`#Max`) stay intact.

---

## Source layout

| File | Role |
|------|------|
| `Token.cs` | token model |
| `XppLexer.cs` | tokenizer (strings/comments/directives consumed whole) |
| `XppFormatter.cs` | Phase A render (braces/indent/spacing/case/chop) + Phase B column alignment + safety verifier |
| `FormatException.cs` | typed lex/balance error → format becomes a no-op |
| `FormatKeyProcessorProvider.cs` / `FormatKeyProcessor.cs` | MEF wiring, `Ctrl+Shift+F`, atomic buffer edit |

`Token/XppLexer/XppFormatter/FormatException` have **no Visual Studio dependency** and are
unit-testable on their own (see `..\Tests\FormatTests`).

## Build & deploy

- Build `JAEEFormatExtension.csproj` (net48). `Directory.Build.targets` copies the output
  to `..\publish\`.
- Deploy with `..\Install-Local.bat` (copies from `publish\` to `EditorComponents\`).
- **Close AX first** — one broken DLL in `EditorComponents` poisons the whole MEF catalog.
  If the DLL is locked by a running client, rename the locked file to a non-`.dll` name
  first, then copy the new one; the next `Ax32.exe` start loads it.

## Tests

`..\Tests\FormatTests` is a small console harness that runs the formatter over a corpus of
real X++ samples and checks idempotency, string-safety, and the layout rules:

```bash
dotnet run --project Tests/FormatTests
```
