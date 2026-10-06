@echo off
setlocal
set "RTT_COMPILER=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%RTT_COMPILER%" (
    echo ERROR: Existing .NET Framework C# compiler was not found.
    exit /b 1
)
if not exist "%~dp0bin" mkdir "%~dp0bin"
"%RTT_COMPILER%" /nologo /target:winexe /platform:anycpu /optimize+ /utf8output /codepage:65001 /out:"%~dp0bin\RTTUnitEditor.exe" /win32manifest:"%~dp0src\app.manifest" /reference:System.dll /reference:System.Core.dll /reference:System.Numerics.dll /reference:System.Web.Extensions.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "%~dp0src\Program.cs" "%~dp0src\NavigationPage.cs" "%~dp0src\MainWindow.cs" "%~dp0src\EditorDialogs.cs" "%~dp0src\BodyEditorView.cs" "%~dp0src\CombatEditorView.cs" "%~dp0src\EditorWorkspace.cs" "%~dp0src\AssemblyEditorView.cs" "%~dp0src\BindingEditorView.cs" "%~dp0src\RelationshipEditor.cs" "%~dp0src\Domain\*.cs" "%~dp0src\Storage\*.cs" "%~dp0src\Editing\EditorSession.cs" "%~dp0src\Editing\AssemblySession.cs" "%~dp0src\Editing\IntegrationBoundaries.cs" "%~dp0src\Legacy\*.cs"
if errorlevel 1 exit /b 1
echo Built: "%~dp0bin\RTTUnitEditor.exe"
exit /b 0
