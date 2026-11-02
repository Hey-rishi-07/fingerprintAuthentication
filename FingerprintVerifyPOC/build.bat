@echo off
REM ==========================================================================
REM  Builds FingerprintVerifyPOC.exe WITHOUT Visual Studio, using the C#
REM  compiler that ships with .NET Framework 4.x on every Windows PC.
REM  Output: .\dist\  (copy this whole folder to the test PC)
REM ==========================================================================
setlocal
cd /d "%~dp0"

set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo .NET Framework 4.x compiler not found: %CSC%
  echo Install .NET Framework 4.5 or newer.
  exit /b 1
)

set LIB=Lib
if not exist dist mkdir dist

"%CSC%" /nologo /target:exe /platform:x86 /optimize+ /out:dist\FingerprintVerifyPOC.exe ^
  /reference:System.dll /reference:System.Core.dll /reference:System.Configuration.dll /reference:System.Data.dll ^
  /reference:"%LIB%\Oracle.ManagedDataAccess.dll" ^
  Program.cs Settings.cs Scanner.cs FingerprintRepository.cs Verifier.cs WebHost.cs WebPage.cs Diagnostics.cs
if errorlevel 1 (
  echo BUILD FAILED
  exit /b 1
)

copy /y App.config                               dist\FingerprintVerifyPOC.exe.config >nul
copy /y "%LIB%\Oracle.ManagedDataAccess.dll"     dist\ >nul
copy /y "%LIB%\MANTRA.MFS100.dll"                dist\ >nul
copy /y "%LIB%\native\x86\MFS100Dll.dll"         dist\ >nul
copy /y "%LIB%\native\x86\iengine_ansi_iso.dll"  dist\ >nul
copy /y Deploy\*.bat                             dist\ >nul

echo.
echo Build OK  ->  %~dp0dist
echo Edit dist\FingerprintVerifyPOC.exe.config (Oracle connection + view name), then run dist\run_web.bat
endlocal
