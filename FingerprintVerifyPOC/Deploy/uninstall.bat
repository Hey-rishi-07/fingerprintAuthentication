@echo off
taskkill /im FingerprintVerifyPOC.exe /f >nul 2>&1
del "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\FingerprintVerifyPOC.lnk" >nul 2>&1
del "%USERPROFILE%\Desktop\Fingerprint Verification.url" >nul 2>&1
rmdir /s /q "%LOCALAPPDATA%\FingerprintVerifyPOC" >nul 2>&1
echo Removed.
pause
