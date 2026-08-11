# Plan — Syntax Highlighter (semantic coloring) extension

Add a MEF **classifier** that colors X++ **types**, **macro references**, and **method
calls** in the AX2012 editor, on top of AX's built-in keyword/string/comment/label
coloring. Colors are configurable via the existing JAEE settings form.

Assembly: `JAEE.AX.EditorExtensions.SyntaxHighlighter` · project folder:
`JAEESyntaxHighlighterExtension\`.

---

## 1. Locked decisions

- **Name:** `JAEESyntaxHighlighterExtension` / assembly `…SyntaxHighlighter`.
- **Three categories**, each an independent classification with its own color + on/off,
  all **on** by default:
  - **Type** → blue `#1F6FC0`
  - **Macro** → red `#C0322C`
  - **Method** → steel-blue `#3C7FB1`
- **Configurable** via the existing registry/file-backed `EditorSettings` singleton,
  edited in the standalone `JAEEEditorExtensionSettingsForm.exe` (there is no VS
  Fonts & Colors dialog in the AX client). A new "Syntax colors" page. Changes take
  effect after an AX restart (same as the other extensions).
- **Layering:** confirmed by the user that a MEF classifier's colors show over the
  built-in colorizer (the brace/word extensions already win), so this is viable.
- **Do NOT** re-color keywords / strings / comments / `@SYS` labels — AX already does
  those. Types/macros/methods are the gap.

---

## 2. Detection spec (heuristic, text-only — no metadata)

Reuses the formatter's `XppLexer` token stream. Precedence: **Macro > Type > Method**.

### Macro (whole token)
- `Directive` tokens (`#define`, `#localmacro`, …) and inline macro refs `#Name`
  (a `Word` token beginning with `#`).

### Type (the whole dotted chain, dots included)
A word, or a dotted chain `A.B.C` (optionally with `<…>` generics or `[]` array), is a
**Type** when it is in a *type position*:
- a **primitive/system keyword**: `int, int64, str, real, boolean, container, date,
  utcdatetime, guid, void, anytype, common, str, real, …` (curated list);
- immediately **before `::`** — `NoYes::`, `System.Drawing.Imaging.ImageFormat::`;
- immediately **after `new`** — `new System.IO.MemoryStream()`;
- **declaration / parameter / field position** — `<chain> <name>` followed by
  `;` / `=` / `,` / `)` (`System.IO.MemoryStream memoryStream;`, `(System.Drawing.Bitmap _bitmap)`);
- **return type** — `[modifiers] <chain> <methodName> (`;
- array form `<chain>[]` (`System.Byte[] byteArray`).

### Method (only the identifier, not the `.`/receiver)
- an identifier immediately **before `(`**, that is **not** a keyword and **not** part of
  a type chain in a type position (so `new …MemoryStream()` stays a Type, not a Method).
- Covers `gdi2wpf(` (definition), `info(`, `_bitmap.MakeTransparent(`,
  `ImageFormat::get_Png(`.

### Explicitly NOT colored
- variables, members after `.` that are not calls, and an enum member after `::` that is
  not a call (`NoYes::false` → `false` uncolored).

### Known limits (heuristic)
- A fully-qualified type used bare in an expression **without** `::`/`new`/decl (e.g.
  `System.Drawing.Color.Red`) is missed — rare in X++.
- A PascalCase variable in an unusual position could occasionally be mis-tagged.
- Block comments `/* … */` spanning lines: per-span tokenization may mis-handle the
  middle of a block comment (v1 accepts this edge case).

---

## 3. Architecture

- **`IClassifierProvider`** `[Export]` `[ContentType("text")]` → returns one
  **`IClassifier`** per buffer.
- **`IClassifier.GetClassificationSpans(SnapshotSpan)`**: take the span's text, tokenize
  with `XppLexer`, run the detection above, and emit a `ClassificationSpan` per
  Type/Macro/Method token, mapping token offset → `SnapshotSpan`. Skip categories whose
  settings flag is off.
- **Classification types & formats:** three `ClassificationTypeDefinition`s
  (`X++ Type`, `X++ Macro`, `X++ Method`) and three `ClassificationFormatDefinition`s
  (`EditorFormatDefinition`) whose `ForegroundColor` is read from
  `EditorSettings.getInstance().SyntaxHighlighter`.
- **Reuse** `Token.cs` + `XppLexer.cs` from the formatter as **linked shared source**
  (like `Tests\FormatTests`). Requires adding a `SourceStart` offset to `Token` and
  setting it in the lexer (harmless to the formatter).
- **Settings:** `JAEESyntaxHighlighterSettings` (colors + enable flags) added to the
  `EditorSettings` singleton; a `SyntaxHighlighterProperties` PropertyGrid object + a new
  page in `AxEditorSettings` (load/save wiring).

---

## 4. Files

**Settings project** (`JAEEEditorExtensionSettings`)
- `JAEESyntaxHighlighterSettings.cs` — **done (S1)**
- `EditorSettings.cs` — field + ctor init **done (S1)**; still need null-safety after load
  for old settings files.

**New classifier project** (`JAEESyntaxHighlighterExtension`)
- `JAEESyntaxHighlighterExtension.csproj` (net48, VS text refs, MEF; link `..\JAEEFormatExtension\Token.cs` + `XppLexer.cs`)
- `Properties\AssemblyInfo.cs` (no version — SharedAssemblyInfo via Directory.Build.targets)
- `ClassificationDefinitions.cs` — 3 `ClassificationTypeDefinition` + 3 format definitions
- `SyntaxClassifierProvider.cs` — `IClassifierProvider`
- `SyntaxClassifier.cs` — `IClassifier` + the detection (pure, unit-testable core)

**Form project** (`JAEEEditorExtensionSettingsForm`)
- `SyntaxHighlighterProperties.cs` (new)
- `AxEditorSettings.cs` + `AxEditorSettings.Designer.cs` — new PropertyGrid page + load/save

**Solution / deploy**
- add the project to `JAEE.AX.EditorExtensions.sln` (so `Build.cmd` builds it)
- add its DLL to `Install-Local.bat`
- (version comes from `SharedAssemblyInfo.cs` — currently 1.2.1.0)

---

## 5. Steps

- **S1 — settings model.** `JAEESyntaxHighlighterSettings` + `EditorSettings` field. *(done; add load-time null-safety.)*
- **S2 — detection core.** `SyntaxClassifier`'s pure classify function over tokens →
  list of (offset, length, category). Add `Token.SourceStart` + lexer offsets. Unit-test
  in `Tests\FormatTests` against the sample method (types/macros/methods spans).
- **S3 — MEF classifier.** Provider + classifier + classification type/format definitions
  reading colors from settings.
- **S4 — csproj + solution.** New project, link shared sources, add to `.sln` +
  `Install-Local.bat`.
- **S5 — settings form page.** `SyntaxHighlighterProperties` + PropertyGrid + load/save.
- **S6 — build.** `Build.cmd` (whole solution) — confirm all projects compile.
- **S7 — runtime test in AX (user-driven).** Close AX → `Install-Local.bat` → reopen;
  verify types/macros/methods color, colors editable via the settings form, categories
  can be turned off, and the classifier colors show over the built-in ones.

---

## 6. Caveats
- The classifier and the WinForms page can only be **compile-verified** here; coloring and
  the settings UI need testing in the AX client (S7).
- Heuristic detection — see §2 limits.
- Changing colors requires an AX restart (the form already says so).
