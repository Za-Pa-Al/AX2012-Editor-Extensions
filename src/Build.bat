@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"

echo ============================================================
echo   JAEE AX2012 Editor Extensions - Build (Release)
echo ============================================================

set "CFG=Release"
set "SLN=%~dp0JAEE.AX.EditorExtensions.sln"
set "RR=%~dp0JAEERefactorRenameExtension\JAEERefactorRenameExtension.csproj"
set "REF=%~dp0..\References"

REM ---------------------------------------------------------------
REM 1. Check the Microsoft VS editor reference DLLs are present.
REM    They are NOT in the repo (see References\README.md) - copy
REM    them from your AX client Bin before building.
REM ---------------------------------------------------------------
set "MISSING="
for %%F in (
  Microsoft.VisualStudio.Text.Data.dll
  Microsoft.VisualStudio.Text.Internal.dll
  Microsoft.VisualStudio.Text.Logic.dll
  Microsoft.VisualStudio.Text.UI.dll
  Microsoft.VisualStudio.Text.UI.Wpf.dll
) do if not exist "%REF%\%%F" ( echo   [missing] References\%%F & set "MISSING=1" )
if not exist "%REF%\EditorComponents\Microsoft.VisualStudio.CoreUtility.dll" (
  echo   [missing] References\EditorComponents\Microsoft.VisualStudio.CoreUtility.dll
  set "MISSING=1"
)
if defined MISSING (
  echo.
  echo [ERROR] Missing Visual Studio editor DLLs. Copy them from your AX client Bin
  echo         ...\Microsoft Dynamics AX\60\Client\Bin  ^(and \EditorComponents^).
  echo         See References\README.md.
  exit /b 1
)

REM ---------------------------------------------------------------
REM 2. Locate MSBuild via vswhere (VS or Build Tools, 2019+).
REM ---------------------------------------------------------------
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
set "MSBUILD="
set "MSBTMP=%TEMP%\_jaee_msbuild.txt"
if exist "%VSWHERE%" (
  "%VSWHERE%" -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" > "%MSBTMP%" 2>nul
  set /p MSBUILD=<"%MSBTMP%"
  del "%MSBTMP%" 2>nul
)
if not defined MSBUILD (
  echo [ERROR] MSBuild not found. Install Visual Studio 2019+ or Build Tools
  echo         with the ".NET desktop build tools" workload.
  exit /b 1
)
echo Using MSBuild: !MSBUILD!
echo.

REM ---------------------------------------------------------------
REM 3. Restore (a separate pass from Build - required so the net48
REM    reference-assembly props are picked up).
REM ---------------------------------------------------------------
echo --- Restoring NuGet packages ---
"!MSBUILD!" "%SLN%" -t:Restore -v:minimal -nologo || goto :fail
"!MSBUILD!" "%RR%"  -t:Restore -v:minimal -nologo || goto :fail

REM ---------------------------------------------------------------
REM 4. Build the solution (7 projects) + RefactorRename (not in .sln).
REM    Directory.Build.targets copies each output to publish\.
REM ---------------------------------------------------------------
echo.
echo --- Building solution (%CFG%) ---
"!MSBUILD!" "%SLN%" -t:Build -p:Configuration=%CFG% -v:minimal -nologo || goto :fail
echo --- Building JAEERefactorRenameExtension (%CFG%) ---
"!MSBUILD!" "%RR%"  -t:Build -p:Configuration=%CFG% -v:minimal -nologo || goto :fail

echo.
echo ============================================================
echo   BUILD OK
echo ============================================================
echo Artifacts in: %~dp0publish
for %%F in ("%~dp0publish\JAEE.AX.EditorExtensions.*.dll" "%~dp0publish\JAEE.AX.EditorExtensions.*.exe") do echo   %%~nxF
echo.
echo Next: run Install-Local.bat to deploy to EditorComponents ^(close AX first^).
exit /b 0

:fail
echo.
echo [BUILD FAILED] See the MSBuild output above.
exit /b 1
