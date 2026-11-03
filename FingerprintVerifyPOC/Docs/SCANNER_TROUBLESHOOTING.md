# Mantra MFS100 scanner: setup and troubleshooting

## 1. Install the driver (admin, once per PC)
1. Run `Driver\MFS100_Driver_Setup.exe` (repo root) **as Administrator** and finish the installer.
2. Plug the MFS100 into a USB port.
3. Open **Device Manager** (`Win + X`). Under *Universal Serial Bus controllers* or *Biometric Devices* you should see **Mantra MFS100 Fingerprint Sensor**.
   * If it has a yellow ⚠️ icon, right-click it, choose *Update driver*, and point it to `C:\Program Files\Mantra\MFS 100\Drivers`.

## 2. Stop the Mantra RD Service / AVDM (very common problem)
The Mantra **RD Service** (installed by the Aadhaar driver bundle from Mantra's website) takes exclusive control of the scanner. While it runs, `Init()` fails with **-1**.

1. Press `Win + R`, type `services.msc`, and press Enter.
2. Find **Mantra AVDM** and **Mantra AVDM Helper**. Right-click each one and choose **Stop**. Then open Properties and set *Startup type* to **Manual** or **Disabled**.
3. Unplug the scanner and plug it back in.

## 3. Test the scanner on its own
Mantra installs a test tool: `C:\Program Files\Mantra\MFS 100\Drivers\MFS100 Test\Mantra.MFS100.Test.exe`.
Run it as Administrator, click **Initialize**, click **Start Capture**, and place a finger. If you see an image and a quality score, the driver and the USB connection are fine. Close the test tool before you start the POC, because only one program can use the scanner at a time.

## 4. Error codes

| Code / message | Meaning | Fix |
|---|---|---|
| `0` | OK | — |
| `MANTRA.MFS100.dll not found` | DLLs are missing next to the exe | Build again with `build.bat` or Visual Studio. The DLLs come from `Lib\`. |
| `-1` on Init | Scanner unplugged, driver missing, or the RD Service/test tool is holding the scanner | See sections 1–3. |
| `-1140` | Timeout: no finger within 10 s | Place the finger while the light is on. |
| `-1324` | Finger removed too early or placed badly | Place it flat and firm, and hold it until the result appears. |
| `BadImageFormatException` | 32-bit/64-bit mismatch | The project must stay **x86**, because the DLLs in `Lib\native\x86` are 32-bit. |
| `PInvokeStackImbalance` (Visual Studio only) | Debugger warning caused by the Mantra native DLL | Start with **Ctrl+F5** (without debugging), or untick it under *Debug → Windows → Exception Settings → Managed Debugging Assistants*. |
