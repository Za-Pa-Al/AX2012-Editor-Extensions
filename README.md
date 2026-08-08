# Microsoft Dynamics AX 2012 X++ Editor Extensions

Initial version of this project is based on MSDN examples for Visual Studio 2010, extending them to the Microsoft Dynamics AX 2012 X++ source code editor.

If you have any idea for improving this extensions, create new ones or you discover a bug, please create an [Issue](https://github.com/jaestevan/AX2012-Editor-Extensions/issues) por make the [changes yourself](https://github.com/jaestevan/AX2012-Editor-Extensions/wiki/Build-the-extensions-and-make-your-own-changes!)!. We are open to collaboration!

## This fork

Modernized to **.NET Framework 4.8** (builds under Build Tools — no VS2010 SDK, no admin) and adds two extensions:

- **Format** — `Ctrl+Shift+F`, X++ *Format Document* (whitespace-only, safety-verified). See [`JAEEFormatExtension/README.md`](JAEE.AX.EditorExtensions/JAEEFormatExtension/README.md).
- **Refactor Rename** — `Ctrl+R`, VS-style buffer-scoped whole-word rename.

**Download the compiled extensions:** [Releases](../../releases/latest) — grab the zip, **close AX**, and copy the DLLs into `…\60\Client\Bin\EditorComponents\` (or run `Install-Local.bat`).

## Building &amp; releasing

- **Build:** open `JAEE.AX.EditorExtensions/JAEE.AX.EditorExtensions.sln`. First place the six Microsoft VS editor DLLs in `References/` — see [`References/README.md`](References/README.md). (`JAEERefactorRenameExtension` isn't in the .sln; build that project on its own.)
- **Version — one place for all assemblies:** [`JAEE.AX.EditorExtensions/SharedAssemblyInfo.cs`](JAEE.AX.EditorExtensions/SharedAssemblyInfo.cs). To ship a new version, edit the two numbers, rebuild, then tag/release. e.g. for 1.3.0:

  ```csharp
  [assembly: AssemblyVersion("1.3.0.0")]
  [assembly: AssemblyFileVersion("1.3.0.0")]
  ```
- **Tests:** `dotnet run --project JAEE.AX.EditorExtensions/Tests/FormatTests`

## Test it! It's super easy:
* [Installing the extensions. Two step guide.](https://github.com/jaestevan/AX2012-Editor-Extensions/wiki/Installing-the-extensions.-Two-step-guide.)

## Take a look to the wiki!
* [Setup and personalize the extensions](https://github.com/jaestevan/AX2012-Editor-Extensions/wiki/Installing-the-extensions.-Two-step-guide.#what-if-i-want-to-setup-some-parameters)
* [Build the extensions and make your own changes!](https://github.com/jaestevan/AX2012-Editor-Extensions/wiki/Build-the-extensions-and-make-your-own-changes!)
* [Troubleshooting & Known issues](https://github.com/jaestevan/AX2012-Editor-Extensions/wiki/Troubleshooting-&-Known-issues)

## How extensions look like?

### Brace Matching Extension

![Brace Matching Extension](../../wiki/images/ax-ext-bracematching.png?raw=true "Brace Matching Extension")

### Highlight Words Extension

![Words Extension](../../wiki/images/ax-ext-highlightword.png?raw=true "Words Extension")

### Outlining Extension

![Outlining Extension](../../wiki/images/ax-ext-outlining-v2.png?raw=true "Outlining Extension")
