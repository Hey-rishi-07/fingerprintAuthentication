@echo off
REM Starts the local service and opens the verification page (http://localhost:8035/)
cd /d "%~dp0"
start "Fingerprint Verification POC" FingerprintVerifyPOC.exe
