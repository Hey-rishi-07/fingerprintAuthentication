@echo off
REM Enrol fingers into the POC table from the console (the web page is at http://localhost:8035/enroll)
cd /d "%~dp0"
FingerprintVerifyPOC.exe --enroll
pause
