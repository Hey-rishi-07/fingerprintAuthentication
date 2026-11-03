@echo off
REM ==========================================================================
REM  Builds the Scanner Agent and the setup package (no Visual Studio needed):
REM     ..\dist\FpScannerAgentSetup\   Setup.bat  Uninstall.bat  README.txt
REM                                   Agent\  (exe, config, Mantra DLLs)
REM                                   Driver\MFS100_Driver_Setup.exe
REM  Zip that folder and put it on the intranet (FpMfa.AgentDownloadUrl).
REM ==========================================================================
setlocal
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" ( echo .NET Framework 4.x compiler not found & exit /b 1 )

set OUT=..\dist\FpScannerAgentSetup
if exist "%OUT%" rmdir /s /q "%OUT%"
mkdir "%OUT%\Agent" "%OUT%\Driver"

"%CSC%" /nologo /target:winexe /platform:x86 /optimize+ /out:"%OUT%\Agent\FpScannerAgent.exe" ^
  /reference:System.dll /reference:System.Core.dll /reference:System.Configuration.dll ^
  AgentProgram.cs ..\FingerprintVerifyPOC\Scanner.cs
if errorlevel 1 ( echo BUILD FAILED & exit /b 1 )

copy /y App.config                               "%OUT%\Agent\FpScannerAgent.exe.config" >nul
copy /y ..\Lib\MANTRA.MFS100.dll                 "%OUT%\Agent\" >nul
copy /y ..\Lib\native\x86\MFS100Dll.dll          "%OUT%\Agent\" >nul
copy /y ..\Lib\native\x86\iengine_ansi_iso.dll   "%OUT%\Agent\" >nul
copy /y ..\Driver\MFS100_Driver_Setup.exe        "%OUT%\Driver\" >nul
copy /y Setup\*.*                                "%OUT%\" >nul

echo.
echo Package ready: %~dp0%OUT%
echo  1. Set AllowedOrigins in %OUT%\Agent\FpScannerAgent.exe.config to your web app address
echo  2. Zip the folder, put it on the intranet, users run Setup.bat once
endlocal
