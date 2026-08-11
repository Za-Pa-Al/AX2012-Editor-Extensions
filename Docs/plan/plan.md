# Plan — VS-style "Refactor Rename" for AX2012 X++ Editor

Status: **DRAFT — awaiting user confirmation.** No code written yet.

## 1. Goal (v1 scope)

Mark an identifier in the AX2012 code editor, press a key (F2), type a new name once,
have all occurrences in the current buffer update together as a single undo.

**In scope (v1):** local variables, buffer-scoped, whole-word + case-sensitive replace,
modal prompt, single undo.

**Out of scope (v1):**
- Inline linked-edit ("type once, all update live"). Modal prompt first.
- String/comment exclusion via `IClassifier`. Accept false positives in v1.
- Cross-method / cross-object rename (needs AX cross-reference — a MEF extension can't reach it).

## 2. Chosen technical approach

| Concern            | Decision                                                                 |
|--------------------|--------------------------------------------------------------------------|
| Entry point        | `IKeyProcessorProvider` MEF export → `KeyProcessor` (native VS editor keyboard hook) |
| Trigger            | Single **Ctrl+R** (intercepted in `KeyProcessor.KeyDown`, before command routing) |
| Fallback trigger   | `IWpfTextViewCreationListener` + `PreviewKeyDown` if KeyProcessor path fails |
| Word under caret   | `ITextStructureNavigator.GetExtentOfWord()` + validity check (copied from HighlightWord) |
| Find occurrences   | `ITextSearchService.FindAll(FindOptions.WholeWord \| FindOptions.MatchCase)` |
| Replace            | `ITextBuffer.CreateEdit()` → `Replace` each → `Apply()` (single undo)     |
| Input              | Modal WPF `Window`, owner = editor window                                |
| New project name   | `JAEERefactorRenameExtension`, assembly `JAEE.AX.EditorExtensions.RefactorRename` |
| Target framework   | **net48** — matches the confirmed machine runtime (4.8, Release 528049); modern BCL, uniform dev fleet |
| Build              | Classic (non-SDK) `.csproj`, **no** VsSDK targets, MSBuild from Build Tools 2022 |
| Targeting pack     | **NuGet `Microsoft.NETFramework.ReferenceAssemblies.net48`** — no admin, no system install |
| References         | HintPath → repo `References\*.dll`, `Private=False`. NB: `CoreUtility.dll` sits in `References\EditorComponents\` |
| FindAll return     | `Collection<SnapshotSpan>` (not `NormalizedSnapshotSpanCollection`) — confirmed by spike |

## 3. Certainty assessment (items below 90% flagged)

| Assumption                                                              | Certainty | Note |
|------------------------------------------------------------------------|-----------|------|
| MEF drop-in load works (JAEE DLLs already do)                          | 95% | Verified by existing extensions |
| `Microsoft.VisualStudio.Text.*` versions match (copied from EditorComponents) | 95% | Matched by construction |
| `CreateEdit/Apply` = single undo, integrates with Ctrl+Z              | 90% | AX ships Undo.Implementation.dll |
| `[ContentType("text")]` catches the X++ buffer                        | 90% | All content types derive from "text" |
| `IKeyProcessorProvider`/`KeyProcessor` extension point available      | 100% ✅ | Compiled against the real DLLs in spike S0.1 |
| Classic csproj net48 + WPF + NuGet refs builds under Build Tools 2022 | 100% ✅ | **PROVEN in spike S0.1** — built clean at both net40 and net48 |
| Single Ctrl+R is free in the AX code editor                          | 95% ✅ | User confirmed "Ctrl+R triggers nothing" |
| **`GetExtentOfWord` treats X++ identifiers with `_`/digits as one word** | **65%** ⚠️ | Default text navigator may split on `_`. X++ may register its own navigator. Runtime test required. |
| Buffer scope == current method                                        | 75% ⚠️ | Handoff author's belief; depends on how AX pane loads code. Verify at runtime. |

**Recommendation:** Do the Step S0 spikes before committing to full build-out.
Two of the three runtime spikes (F2, navigator) need a running AX client — I cannot run those.

## 4. Actionable steps

### Step S0 — De-risk spikes
- [x] **S0.1 Build toolchain — DONE ✅.** Classic `.csproj` + NuGet `ReferenceAssemblies` (net40 and net48)
      + WPF + 6 References DLLs, using real API types. Built clean → DLL at both targets. No admin. **Target = net48.**
- [x] **S0.2 Ctrl+R conflict — DONE ✅.** User confirmed Ctrl+R triggers nothing in the AX editor.
- [ ] **S0.3 Word extent (needs AX, deferred to runtime test):** confirm identifier boundaries.
      Robust handling built in from the start (see Step 3) — navigator + self-extend guard.

### Step 1 — Scaffold project
- [x] **DONE ✅** `JAEE.AX.EditorExtensions\JAEERefactorRenameExtension\` created
- [x] **DONE ✅** `JAEERefactorRenameExtension.csproj` (classic net48, no VsSDK import)
- [x] **DONE ✅** `Properties\AssemblyInfo.cs`
- [x] **DONE ✅** HintPath references + WPF references, all `Private=False`

### Step 2 — Keyboard hook (MEF)
- [x] **DONE ✅** `RefactorRenameKeyProcessorProvider` in `RefactorRenameKeyProcessorProvider.cs`
- [x] **DONE ✅** `RefactorRenameKeyProcessor` in `RefactorRenameKeyProcessor.cs`

### Step 3 — Rename core
- [x] **DONE ✅** Caret → `GetExtentOfWord` → validity + retry −1 char
- [x] **DONE ✅** Self-extend guard (`SelfExtend`) for `_`-split identifiers
- [x] **DONE ✅** `FindAll(WholeWord|MatchCase)` → `Collection<SnapshotSpan>`
- [x] **DONE ✅** `CreateEdit()` → `Replace` each span → `Apply()`

### Step 4 — Input dialog
- [x] **DONE ✅** `RenameDialog.cs` — code-only WPF Window, prefilled + SelectAll on load
- [x] **DONE ✅** Enter = OK (IsDefault), Esc = Cancel (IsCancel + KeyDown), Owner set
- [x] **DONE ✅** Validation: `^[A-Za-z_][A-Za-z0-9_]*$`, returns null if unchanged

### Step 5 — Build & package
- [x] **DONE ✅** `msbuild /p:Configuration=Release /restore` — built clean, 11 KB DLL
- [x] **DONE ✅** `Install-Local.bat` added (builds Release + copies to EditorComponents)

### Step 6 — Runtime test (user, in AX)
- [ ] Method with local var used in code **+ comment + string literal**
- [ ] Verify: replace-all correct, single Ctrl+Z reverts all, false-positive behavior in comment/string
- [ ] Verify: trigger key no conflict; dialog owner correct (appears in front)

### Step 7 — Docs & commit
- [ ] Update `README.md`
- [ ] git commit (repo is a live clone; commit message without AI attribution per guideline)

## 5. Decisions — RESOLVED (technically best)

1. **Trigger key:** ✅ Single **Ctrl+R** via `IKeyProcessorProvider` → `KeyProcessor`.
   Native editor keyboard hook, claims the key before command routing. PreviewKeyDown kept as fallback only.
2. **Navigator handling:** ✅ Navigator first, with a **self-extend guard** (Step 3) that fixes any
   `_`-splitting. More robust than either navigator-only or regex-only.
3. **Solution integration:** ✅ **Standalone** — own classic `.csproj`, built directly via `msbuild`,
   plus its own minimal `.sln`. Avoids the VsSDK build coupling that blocks the other 5 projects
   under Build Tools 2022.
4. **Settings:** ✅ **Hardcode Ctrl+R for v1** behind a single constant (clean seam).
   Wiring into the `EditorSettings` SOAP singleton deferred to v2 — no value now, adds coupling.

## 6. Known limitations carried into v1
- `\b`-style whole-word match also hits the identifier inside strings and comments.
- Buffer-scoped only — correct for locals, not fields/methods across objects.
- No live preview of affected occurrences before applying.
