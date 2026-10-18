@echo off
rem MapleStory Classic EXP Tracker. Copyright (c) 2026 Aydrian. All rights reserved. See LICENSE.
rem Builds MapleExpTracker.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
rem   build.bat              build the tracker
rem   build.bat test         build the tracker, then build and run the tests
rem   build.bat docs [dir]   build the tracker, then draw the download-page images into dir (default: docs)
setlocal
cd /d "%~dp0"

set "FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
if not exist "%FW%\csc.exe" set "FW=%WINDIR%\Microsoft.NET\Framework\v4.0.30319"
if not exist "%FW%\csc.exe" (
  echo Could not find the .NET Framework 4 C# compiler ^(csc.exe^).
  exit /b 1
)
set "WM=%WINDIR%\System32\WinMetadata"
set REFS=/r:"%FW%\System.Runtime.dll" /r:"%WM%\Windows.Foundation.winmd" /r:"%WM%\Windows.Globalization.winmd" /r:"%WM%\Windows.Graphics.winmd" /r:"%WM%\Windows.Media.winmd" /r:"%WM%\Windows.Storage.winmd"
set ICON=
if exist src\app.ico set ICON=/win32icon:src\app.ico

"%FW%\csc.exe" /nologo /target:winexe /optimize+ /out:MapleExpTracker.exe /win32manifest:src\app.manifest %ICON% %REFS% src\*.cs
if errorlevel 1 exit /b 1
echo Built MapleExpTracker.exe

if /i "%~1"=="docs" goto docs
if /i not "%~1"=="test" exit /b 0

"%FW%\csc.exe" /nologo /target:exe /out:tests\RunTests.exe /win32manifest:src\app.manifest %REFS% tests\Tests.cs src\ExpParser.cs src\ExpTable.cs src\ExpTracker.cs src\Fmt.cs src\OcrService.cs src\PixelFontReader.cs
if errorlevel 1 exit /b 1
tests\RunTests.exe %2 %3 %4 %5
exit /b %errorlevel%

:docs
set "OUT=%~2"
if "%OUT%"=="" set "OUT=docs"
setlocal enabledelayedexpansion
set DOCSRC=
for %%f in (src\*.cs) do if /i not "%%~nxf"=="Program.cs" set DOCSRC=!DOCSRC! "%%f"
"%FW%\csc.exe" /nologo /target:exe /out:tools\DocImages.exe /win32manifest:src\app.manifest %REFS% tools\DocImages.cs !DOCSRC!
if errorlevel 1 exit /b 1
tools\DocImages.exe "%OUT%" tests\fixtures
