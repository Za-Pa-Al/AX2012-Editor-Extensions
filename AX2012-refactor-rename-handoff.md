# AX2012 — Build a VS-style "Refactor Rename" — Handoff

## Goal
Add a Visual-Studio-style **Rename** to the AX2012 X++ code editor. Mark an identifier (primarily a **local variable**), trigger a command, type a new name once, and have all occurrences update — without the dangerous global Search & Replace.

## Key context (important, non-obvious)
- The **AX2012 code editor is the Visual Studio editor engine**. It loads **MEF editor extensions** as DLLs dropped into:
  `C:\Program Files (x86)\Microsoft Dynamics AX\60\Client\Bin\EditorComponents`
- The editor is **hosted without the full VS shell**, so VS command/menu routing (`IOleCommandTarget`) may not be wired up. Do **not** assume it works.
- Existing extensions already installed in that folder (reference material):
  - `JAEE.AX.EditorExtensions.HighlightWord.dll` — highlights all occurrences of the word under the caret. **This already solves the hard half of rename** (finding the current identifier + its occurrences).
  - `JAEE.AX.EditorExtensions.BraceMatching.dll`
  - `JAEE.AX.EditorExtensions.Outlining.dll`
  - Source is José Antonio Estevan's project, originally on CodePlex (`ax2012editorext.codeplex.com`). **CodePlex is dead (shut down 2017)** — source may now be on GitHub or the CodePlex archive. **dotPeek is available** on this machine to decompile the DLLs directly.

## Chosen approach
Build/fork a **C# MEF editor extension** that adds an in-buffer rename. Reuse HighlightWord's view-hook and word-extent logic (decompile it with dotPeek — don't rely on memory of the API).

Rejected alternatives:
- **X++ editor script** (`EditorScripts` class): works, but modal and clunky; no VS-like UX. Fallback only.
- **Global Search & Replace**: unsafe (the thing being avoided).
- **Reusing the highlight feature directly**: it's visual-only (a MEF tagger); exposes no API to call.

## Implementation plan

### 1. Trigger (reliable path)
Use a **WPF key handler on the text view** — pure WPF, does not depend on the VS shell:
1. MEF-export an `IWpfTextViewCreationListener` (same entry point HighlightWord uses).
2. On `TextViewCreated`, subscribe to `view.VisualElement.PreviewKeyDown`.
3. On the chosen key (F2 = VS convention): get current word → prompt for new name → replace.

Get the word under the caret via the text structure navigator (`ITextStructureNavigatorSelectorService`) — confirm exact usage from HighlightWord in dotPeek.

### 2. Replace core (atomic, single undo)
```csharp
var snapshot = _view.TextBuffer.CurrentSnapshot;
string text  = snapshot.GetText();
var matches  = Regex.Matches(text, @"\b" + Regex.Escape(oldName) + @"\b");

using (var edit = _view.TextBuffer.CreateEdit())
{
    foreach (Match m in matches)
        edit.Replace(m.Index, m.Length, newName);
    edit.Apply();   // all occurrences update together, single undo
}
```

### 3. New-name input
Start with a **simple modal WPF prompt** on F2. True inline "type once, all update" (linked edits) is possible but you must synchronize the spans yourself — the VS2010-era editor has no simple built-in rename primitive. Ship the prompt first; add inline later if wanted.

## Open items to verify (do not assume)
- **Assembly versions**: the extension must reference the `Microsoft.VisualStudio.Text.*` / MEF assemblies matching the editor version AX2012 embeds (VS2010-era, believed — **verify**). Wrong versions = the DLL won't load. Check the versions the existing JAEE DLLs reference (dotPeek).
- Whether `IOleCommandTarget`/VS command routing is available in the AX host (assume **no**; use the WPF key hook).
- Exact `ITextStructureNavigator` usage for word extent — confirm from HighlightWord.
- `IClassifier` access if implementing the strings/comments exclusion below.

## Known limitations / caveats
- The `\b` regex also matches the identifier **inside string literals and comments**. Fine for most locals. To skip them, query the editor's `IClassifier` (the syntax-coloring component) and drop matches in comment/string spans — verify that API first.
- **Buffer-scoped = current method only.** Correct for local variables. Does **not** cover class fields or method names across objects — that needs the AX cross-reference, which a MEF extension can't easily reach. Keep the first version scoped to locals.

## Deployment
Built DLL goes into each developer's `Client\Bin\EditorComponents` folder. The client picks it up on start. If a downloaded zip is "blocked" by Windows, Unblock it before unzipping (dlls inherit the blocked flag and won't load).

## Suggested first milestone
Local-variable rename: F2 → prompt → whole-word replace-all in the current buffer, single undo. Verify against a method with the variable also appearing in a comment/string to see the known false-positive behavior before deciding whether to add classifier filtering.
