@echo off
REM ==========================================================================
REM  Installs the POC for the current Windows user (no admin needed):
REM   - copies files to %LOCALAPPDATA%\FingerprintVerifyPOC
REM   - starts it minimised at every logon (local service on http://localhost:8035/)
REM   - puts a "Fingerprint Verification" link on the desktop
REM  The Mantra MFS100 driver must be installed separately (admin, once per PC).
REM ==========================================================================
setlocal
set "SRC=%~dp0"
set "DEST=%LOCALAPPDATA%\FingerprintVerifyPOC"

echo Installing to %DEST% ...
taskkill /im FingerprintVerifyPOC.exe /f >nul 2>&1
if not exist "%DEST%" mkdir "%DEST%"

REM keep an existing config (DB settings) when re-installing
if exist "%DEST%\FingerprintVerifyPOC.exe.config" copy /y "%DEST%\FingerprintVerifyPOC.exe.config" "%TEMP%\fpv.config.bak" >nul
xcopy /y /q "%SRC%*.*" "%DEST%\" >nul
if exist "%TEMP%\fpv.config.bak" move /y "%TEMP%\fpv.config.bak" "%DEST%\FingerprintVerifyPOC.exe.config" >nul

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$sh = New-Object -ComObject WScript.Shell;" ^
  "$lnk = $sh.CreateShortcut([Environment]::GetFolderPath('Startup') + '\FingerprintVerifyPOC.lnk');" ^
  "$lnk.TargetPath = '%DEST%\FingerprintVerifyPOC.exe'; $lnk.Arguments = '--no-browser';" ^
  "$lnk.WorkingDirectory = '%DEST%'; $lnk.WindowStyle = 7; $lnk.Save();" ^
  "Set-Content -Path ([Environment]::GetFolderPath('Desktop') + '\Fingerprint Verification.url') -Value ('[InternetShortcut]' + [char]13 + [char]10 + 'URL=http://localhost:8035/')"

start "" /min "%DEST%\FingerprintVerifyPOC.exe" --no-browser
echo.
echo Done. Starts automatically at logon. Page: http://localhost:8035/
echo DB settings: %DEST%\FingerprintVerifyPOC.exe.config
endlocal
pause
