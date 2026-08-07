@echo off
set EditorComponents="C:\Program Files (x86)\Microsoft Dynamics AX\60\Client\Bin\EditorComponents"
set MSBuild="C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"

echo Building...
%MSBuild% JAEERefactorRenameExtension.csproj /p:Configuration=Release /restore /nologo /v:minimal
if errorlevel 1 (
    echo BUILD FAILED
    pause
    exit /b 1
)

echo Copying to EditorComponents...
copy /Y bin\Release\JAEE.AX.EditorExtensions.RefactorRename.dll %EditorComponents%
if errorlevel 1 (
    echo COPY FAILED - run as administrator or check the path
    pause
    exit /b 1
)

echo Done. Restart the AX client to load the extension.
pause
