@echo off
REM ==========================================================================
REM  Builds FingerprintMfa.dll and collects everything to copy into a WebForms app:
REM    ..\dist\WebIntegration\
REM       bin\FingerprintMfa.dll, bin\Oracle.ManagedDataAccess.dll
REM       bin\mantra\MANTRA.MFS100.dll + x86\ + x64\   (server-side matching)
REM       FingerprintVerify.aspx, FingerprintAdmin.aspx, FingerprintMfa.ashx
REM       Sql\, Samples\
REM ==========================================================================
setlocal
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" ( echo .NET Framework 4.x compiler not found & exit /b 1 )
set FW=%WINDIR%\Microsoft.NET\Framework\v4.0.30319
set OUT=..\dist\WebIntegration
if exist "%OUT%" rmdir /s /q "%OUT%"
mkdir "%OUT%\bin\mantra\x86" "%OUT%\bin\mantra\x64" "%OUT%\Sql" "%OUT%\Samples" "%OUT%\Downloads"

"%CSC%" /nologo /target:library /platform:anycpu /optimize+ /out:"%OUT%\bin\FingerprintMfa.dll" ^
  /reference:System.dll /reference:System.Core.dll /reference:System.Configuration.dll /reference:System.Data.dll ^
  /reference:"%FW%\System.Web.dll" /reference:"%FW%\System.Web.Extensions.dll" ^
  /reference:..\Lib\Oracle.ManagedDataAccess.dll ^
  FingerprintMfa\MfaConfig.cs FingerprintMfa\TemplateCodec.cs FingerprintMfa\ServerMatcher.cs ^
  FingerprintMfa\MfaRepository.cs FingerprintMfa\MfaLogin.cs FingerprintMfa\FingerprintHandler.cs
if errorlevel 1 ( echo BUILD FAILED & exit /b 1 )

copy /y ..\Lib\Oracle.ManagedDataAccess.dll      "%OUT%\bin\" >nul
copy /y ..\Lib\MANTRA.MFS100.dll                 "%OUT%\bin\mantra\" >nul
copy /y ..\Lib\native\x86\*.dll                  "%OUT%\bin\mantra\x86\" >nul
copy /y ..\Lib\native\x64\*.dll                  "%OUT%\bin\mantra\x64\" >nul
copy /y CopyToWebApp\*.*                         "%OUT%\" >nul
copy /y Sql\*.*                                  "%OUT%\Sql\" >nul
copy /y Samples\*.*                              "%OUT%\Samples\" >nul
echo Put FpScannerAgentSetup.zip here (build it with ScannerAgent\build.bat) > "%OUT%\Downloads\README.txt"

echo.
echo Done: %~dp0%OUT%
echo Copy bin\, the .aspx/.ashx files and Downloads\ into your web application (see README.md)
endlocal
