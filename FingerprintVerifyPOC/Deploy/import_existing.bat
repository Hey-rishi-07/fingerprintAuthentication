@echo off
REM Copies existing fingerprints from ImportSourceTable (main view) into the POC table FP_POC_ENROLL.
REM Usage: import_existing.bat            (all PC Nos)
REM        import_existing.bat 123456     (one PC No)
cd /d "%~dp0"
if "%~1"=="" (FingerprintVerifyPOC.exe --import ALL) else (FingerprintVerifyPOC.exe --import %1)
pause
