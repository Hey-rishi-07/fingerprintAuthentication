@echo off
REM ==========================================================================
REM  Fingerprint Scanner Agent - one-time setup (run once per PC)
REM   1. installs the Mantra MFS100 driver (optional, asks)
REM   2. installs the agent to Program Files
REM   3. starts it hidden at every logon for every user (no window)
REM   4. registers fpscanneragent:// so the web page can (re)start it
REM  Needs administrator rights - it asks for them automatically.
REM ==========================================================================
setlocal
net session >nul 2>&1
if %errorlevel% neq 0 (
  echo Requesting administrator rights...
  powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)

set "SRC=%~dp0"
set "DEST=%ProgramFiles%\FpScannerAgent"
set "PORT=8036"
echo.
echo  Fingerprint Scanner Agent setup
echo  ===============================

REM ---- 1. Mantra driver --------------------------------------------------
if exist "%SRC%Driver\MFS100_Driver_Setup.exe" (
  choice /c YN /m " Install / repair the Mantra MFS100 scanner driver now"
  if not errorlevel 2 (
    echo  Running driver setup - finish the installer window, then setup continues...
    start "" /wait "%SRC%Driver\MFS100_Driver_Setup.exe"
  )
)

REM ---- 2. Agent files ----------------------------------------------------
echo  Installing agent to %DEST% ...
taskkill /im FpScannerAgent.exe /f >nul 2>&1
timeout /t 1 /nobreak >nul
if not exist "%DEST%" mkdir "%DEST%"
REM keep an existing config (e.g. AllowedOrigins set by IT) when re-installing
if exist "%DEST%\FpScannerAgent.exe.config" copy /y "%DEST%\FpScannerAgent.exe.config" "%TEMP%\fpagent.config.bak" >nul
xcopy /y /q "%SRC%Agent\*.*" "%DEST%\" >nul
if errorlevel 1 ( echo  ERROR: could not copy files. & goto :end )
if exist "%TEMP%\fpagent.config.bak" move /y "%TEMP%\fpagent.config.bak" "%DEST%\FpScannerAgent.exe.config" >nul
powershell -NoProfile -Command "Get-ChildItem -LiteralPath '%DEST%' -Recurse | Unblock-File" >nul 2>&1

REM ---- 3. Allow the local port for every user ----------------------------
netsh http delete urlacl url=http://localhost:%PORT%/ >nul 2>&1
netsh http delete urlacl url=http://127.0.0.1:%PORT%/ >nul 2>&1
netsh http add urlacl url=http://localhost:%PORT%/ sddl=D:(A;;GX;;;WD) >nul
netsh http add urlacl url=http://127.0.0.1:%PORT%/ sddl=D:(A;;GX;;;WD) >nul

REM ---- 4. Start at logon (all users) + fpscanneragent:// link -------------
reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" /v FpScannerAgent /t REG_SZ /d "\"%DEST%\FpScannerAgent.exe\"" /f >nul
reg add "HKLM\SOFTWARE\Classes\fpscanneragent" /ve /d "URL:Fingerprint Scanner Agent" /f >nul
reg add "HKLM\SOFTWARE\Classes\fpscanneragent" /v "URL Protocol" /d "" /f >nul
reg add "HKLM\SOFTWARE\Classes\fpscanneragent\shell\open\command" /ve /d "\"%DEST%\FpScannerAgent.exe\" \"%%1\"" /f >nul

REM ---- 5. Start it now as the normal (non-admin) user ---------------------
explorer.exe "%DEST%\FpScannerAgent.exe"
echo  Starting agent...
timeout /t 4 /nobreak >nul
powershell -NoProfile -Command "try { (New-Object Net.WebClient).DownloadString('http://localhost:%PORT%/status') } catch { 'Agent did not answer yet: ' + $_.Exception.Message }"
echo.
echo  Done. The agent now runs in the background at every logon.
echo  Status page: http://localhost:%PORT%/status     Log: %%LOCALAPPDATA%%\FpScannerAgent\agent.log
:end
echo.
pause
endlocal
