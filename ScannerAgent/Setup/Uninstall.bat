@echo off
REM Removes the Fingerprint Scanner Agent (the Mantra driver is left installed).
setlocal
net session >nul 2>&1
if %errorlevel% neq 0 (
  powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)
set "DEST=%ProgramFiles%\FpScannerAgent"
taskkill /im FpScannerAgent.exe /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" /v FpScannerAgent /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Classes\fpscanneragent" /f >nul 2>&1
netsh http delete urlacl url=http://localhost:8036/ >nul 2>&1
netsh http delete urlacl url=http://127.0.0.1:8036/ >nul 2>&1
timeout /t 1 /nobreak >nul
rmdir /s /q "%DEST%" >nul 2>&1
echo Fingerprint Scanner Agent removed.
pause
endlocal
