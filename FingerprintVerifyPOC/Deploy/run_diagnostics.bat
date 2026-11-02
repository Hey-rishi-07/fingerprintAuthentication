@echo off
REM Explains why a PC No does not match: stored template format, live scores, ISO vs ANSI.
REM The report is saved in logs\diag_*.txt
cd /d "%~dp0"
FingerprintVerifyPOC.exe --diag
