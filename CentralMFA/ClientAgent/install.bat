@echo off
:: ====================================================================
:: Mantra MFS100 Scanner Agent Auto-Installer (Port 8034)
:: ====================================================================
echo -------------------------------------------------------------
echo  Installing Mantra MFS100 Scanner Background Agent
echo -------------------------------------------------------------

:: 1. Target directory in Program Files
set TARGET_DIR=C:\Program Files\MfaScannerAgent
mkdir "%TARGET_DIR%" 2>nul

:: 2. Copy application files
echo Copying agent files to %TARGET_DIR%...
copy /Y "%~dp0MfaScannerAgent.exe" "%TARGET_DIR%\" >nul
copy /Y "%~dp0MANTRA.MFS100.dll" "%TARGET_DIR%\" >nul
copy /Y "%~dp0MFS100Dll.dll" "%TARGET_DIR%\" >nul
copy /Y "%~dp0iengine_ansi_iso.dll" "%TARGET_DIR%\" >nul

:: 3. Configure Windows Auto-Start on Boot
echo Registering auto-start on Windows boot...
reg add "HKLM\Software\Microsoft\Windows\CurrentVersion\Run" /v "MfaScannerAgent" /t REG_SZ /d "\"%TARGET_DIR%\MfaScannerAgent.exe\"" /f >nul
if %errorlevel% neq 0 (
    echo [Notice] Could not write to HKLM (Admin rights required). Adding to User Startup instead...
    reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "MfaScannerAgent" /t REG_SZ /d "\"%TARGET_DIR%\MfaScannerAgent.exe\"" /f >nul
)

:: 4. Start the Agent now
echo Starting MfaScannerAgent on Port 8034...
start "" "%TARGET_DIR%\MfaScannerAgent.exe"

echo -------------------------------------------------------------
echo  Installation Successful!
echo  The scanner agent is now running locally on http://127.0.0.1:8034
echo -------------------------------------------------------------
timeout /t 5
