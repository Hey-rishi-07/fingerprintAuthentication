# FpScannerAgent: fingerprint scanner helper for user PCs

A background program with no window. It lets intranet web applications use the Mantra MFS100 scanner. It **only captures** fingerprints: no database access, no table names, no matching.

| Endpoint | Purpose |
|---|---|
| `GET http://localhost:8036/status` | `{"agent":"FpScannerAgent","scannerReady":true,...}` |
| `POST http://localhost:8036/capture` `{"timeout":10,"blink":true}` | The light blinks, then waits for the finger. Returns `{"ok":true,"quality":78,"iso":"<base64>","ansi":"<base64>"}`. |

Only origins listed in `AllowedOrigins` (in `FpScannerAgent.exe.config`) can call it.

## Build the installer
```
ScannerAgent\build.bat   ->  dist\FpScannerAgentSetup\
                               Setup.bat  Uninstall.bat  README.txt
                               Agent\   FpScannerAgent.exe + .config + Mantra DLLs
                               Driver\  MFS100_Driver_Setup.exe
```
1. Set `AllowedOrigins` in `dist\FpScannerAgentSetup\Agent\FpScannerAgent.exe.config` to your web app address(es).
2. Zip the folder and put it on the intranet, for example in the web app's `Downloads\` folder.

## Install on a PC (once)
Run `Setup.bat` and accept the admin prompt. It:
1. Asks whether to install the **Mantra driver** (press Y the first time).
2. Copies the agent to `C:\Program Files\FpScannerAgent` and unblocks the files.
3. Allows port 8036 for all users (`netsh http add urlacl`).
4. Starts the agent **hidden at every logon for every user** (HKLM `Run` key).
5. Registers `fpscanneragent://`, so the web page can restart the agent with one click.
6. Starts it now and shows the status.

Re-running `Setup.bat` updates the agent and keeps the existing config. `Uninstall.bat` removes it but leaves the driver.

The agent log is at `%LOCALAPPDATA%\FpScannerAgent\agent.log`.

For many PCs, IT can run `Setup.bat` through SCCM, Intune or GPO. The driver installer may show its own window.
