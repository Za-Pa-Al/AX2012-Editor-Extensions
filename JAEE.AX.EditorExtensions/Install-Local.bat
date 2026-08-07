@echo off
set Publish=%~dp0publish
set EditorComponents=C:\Program Files (x86)\Microsoft Dynamics AX\60\Client\Bin\EditorComponents

copy /Y "%Publish%\JAEE.AX.EditorExtensions.EditorSettings.dll"        "%EditorComponents%\"
copy /Y "%Publish%\JAEE.AX.EditorExtensions.EditorSettingsForm.exe"    "%EditorComponents%\"
copy /Y "%Publish%\JAEE.AX.EditorExtensions.BraceMatching.dll"         "%EditorComponents%\"
copy /Y "%Publish%\JAEE.AX.EditorExtensions.HighlightWord.dll"         "%EditorComponents%\"
copy /Y "%Publish%\JAEE.AX.EditorExtensions.Outlining.dll"             "%EditorComponents%\"
copy /Y "%Publish%\JAEE.AX.EditorExtensions.CurrentLineHighlight.dll"  "%EditorComponents%\"
copy /Y "%Publish%\JAEE.AX.EditorExtensions.RefactorRename.dll"        "%EditorComponents%\"
copy /Y "%Publish%\JAEE.AX.EditorExtensions.Format.dll"                "%EditorComponents%\"

echo Deployed to: %EditorComponents%
pause
