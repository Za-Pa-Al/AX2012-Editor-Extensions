# References

The projects reference six Microsoft Visual Studio editor assemblies via `HintPath`.
These are **Microsoft redistributables**, not part of this MIT-licensed project, so they
are **not committed** to the repository (`.gitignore` excludes `References/**/*.dll`).

To build locally, place the following DLLs here (copy them from your **AX2012 client
`Bin`** — the same assemblies the editor loads at runtime):

```
References/
    Microsoft.VisualStudio.Text.Data.dll
    Microsoft.VisualStudio.Text.Internal.dll
    Microsoft.VisualStudio.Text.Logic.dll
    Microsoft.VisualStudio.Text.UI.dll
    Microsoft.VisualStudio.Text.UI.Wpf.dll
    EditorComponents/
        Microsoft.VisualStudio.CoreUtility.dll
```

Typical source paths on a machine with the AX2012 client installed:

- `…\Microsoft Dynamics AX\60\Client\Bin\` — the five `Microsoft.VisualStudio.Text.*` DLLs
- `…\Microsoft Dynamics AX\60\Client\Bin\EditorComponents\` — `Microsoft.VisualStudio.CoreUtility.dll`

All references use `Private=False` (they are not copied to build output — the editor
already provides them at runtime).
