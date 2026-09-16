# Mantra MFS100: Required Files, Drivers, and Setup Guide

This guide details all necessary drivers, native libraries, and configuration files required for the **Mantra MFS100 fingerprint scanner** to be properly detected and functional in the application.

---

## 1. Quick Summary Checklist

When deploying or running the application on a target PC (e.g., your company machine):

- [ ] **Run Driver Setup**: Install `MFS100_Driver_Setup.exe` (installs USB driver `MFS100.sys` and C++ runtimes).
- [ ] **Plug in USB Scanner**: Verify Windows Device Manager shows `Mantra MFS100 Fingerprint Sensor`.
- [ ] **Check Background Services**: Ensure `Mantra AVDM` / `RD Service` is **Stopped** (so it doesn't hold the USB port).
- [ ] **Verify `bin\` Folder**: Ensure `MANTRA.MFS100.dll`, `MFS100Dll.dll`, `iengine_ansi_iso.dll`, and `Oracle.ManagedDataAccess.dll` are in `Authentication\bin\`.
- [ ] **Check Bitness**: Ensure the native DLLs in `bin\` match your IIS Express bitness (x86 or x64).

---

## 2. Level 1: Windows System & Driver Requirements (Hardware Level)

Before any .NET code can communicate with the scanner, Windows must recognize the physical USB device.

### A. Driver Installer (`MFS100_Driver_Setup.exe`)
- **What it does**: Installs the low-level kernel USB drivers (`MFS100.sys`), registers USB Vendor/Product IDs (`VID_27FD&PID_0100`), and installs the Visual C++ runtime dependencies.
- **Where to find it**:
  - In this repository: `MantraAttendance\MantraAttendance\MFS100_Driver_Setup.zip`
- **Installation**:
  1. Extract and run `MFS100_Driver_Setup.exe` as **Administrator**.
  2. Follow the on-screen installer to completion.
  3. Plug the Mantra MFS100 into a USB 2.0 or 3.0 port.

### B. Hardware Verification in Device Manager
1. Press `Win + X` → select **Device Manager**.
2. Expand **Universal Serial Bus controllers** (or **Biometric Devices**).
3. Confirm you see:
   ```text
   Mantra MFS100 Fingerprint Sensor
   ```
4. If there is a yellow exclamation mark `⚠️`, right-click → *Update driver* → point to `C:\Program Files\Mantra\MFS 100\Drivers`.

### C. Critical: Stop Mantra RD Service (Avoid Device Locking)
- If you downloaded the driver bundle from the official Mantra website, it may have installed **Mantra AVDM (RD Service)**.
- **Why this matters**: RD Service is designed for government Aadhaar authentication and continuously holds exclusive control of the USB scanner. If running, raw SDK calls will fail with error code `-1`.
- **How to disable it**:
  1. Press `Win + R`, type `services.msc`, and hit Enter.
  2. Locate:
     - **Mantra AVDM**
     - **Mantra AVDM Helper**
  3. Right-click each → **Stop**.
  4. Right-click → Properties → set **Startup type** to **Disabled** or **Manual**.
  5. Unplug and replug the MFS100 scanner.

### D. Testing the Scanner Independently
Mantra installs an official diagnostic test tool:
```text
C:\Program Files\Mantra\MFS 100\Drivers\MFS100 Test\Mantra.MFS100.Test.exe
```
Run this tool as Administrator:
1. Click **Initialize**.
2. Click **Start Capture**.
3. Place your finger on the optical sensor.
4. If the fingerprint image appears and shows a quality score, your driver and USB hardware are 100% operational.

---

## 3. Level 2: Application Level (Files in the `bin\` Folder)

For the ASP.NET WebForms application to detect the scanner and run fingerprint matching, the following **DLL files** must be located in your application's **`bin\`** directory:

| File Name | Architecture | Type | Purpose | Size |
| :--- | :--- | :--- | :--- | :--- |
| **`MANTRA.MFS100.dll`** | AnyCPU (x86/x64) | .NET Assembly | High-level managed wrapper exposing `Init()`, `AutoCapture()`, `MatchISO()`, etc. | ~8.5 MB |
| **`MFS100Dll.dll`** | x86 or x64 | Native C++ DLL | Hardware communication layer talking to the USB driver. | ~28 KB (x86)<br/>~34 KB (x64) |
| **`iengine_ansi_iso.dll`** | x86 or x64 | Native C++ DLL | Innovatrics biometric engine for ISO 19794-2 template generation and matching. | ~899 KB (x86)<br/>~1.1 MB (x64) |
| **`Oracle.ManagedDataAccess.dll`** | AnyCPU | .NET Assembly | Oracle 11g database driver (pure managed, no Oracle client needed). | ~4.5 MB |
| **`Authentication.dll`** | AnyCPU | .NET Assembly | The compiled web application code. | ~39 KB |

---

## 4. Architecture Alignment (x86 vs x64)

The native DLLs (`MFS100Dll.dll` and `iengine_ansi_iso.dll`) must match the bitness of your running IIS Express worker process:

```text
Authentication/
└── Lib/
    ├── MANTRA.MFS100.dll
    ├── Oracle.ManagedDataAccess.dll
    └── native/
        ├── x86/
        │   ├── MFS100Dll.dll          <-- For 32-bit IIS Express
        │   └── iengine_ansi_iso.dll   <-- For 32-bit IIS Express
        └── x64/
            ├── MFS100Dll.dll          <-- For 64-bit IIS Express (Recommended)
            └── iengine_ansi_iso.dll   <-- For 64-bit IIS Express (Recommended)
```

### Which One Should You Use?

| Scenario | IIS Express Path | Required Native DLLs |
| :--- | :--- | :--- |
| **Visual Studio 64-bit IIS Express (Recommended)** | `C:\Program Files\IIS Express\iisexpress.exe` | Copy files from `Lib\native\x64\` to `bin\` |
| **Visual Studio 32-bit IIS Express** | `C:\Program Files (x86)\IIS Express\iisexpress.exe` | Copy files from `Lib\native\x86\` to `bin\` |

> **Note on Visual Studio Configuration**:  
> To enable 64-bit IIS Express: Go to **Tools** → **Options** → **Projects and Solutions** → **Web Projects** → check **"Use the 64-bit version of IIS Express for web sites and projects"**.

---

## 5. Deployment Steps for Your Company PC

1. **Copy Folder**: Copy the entire `Authentication` folder to your company PC (e.g. `C:\Projects\Authentication` or `D:\Authentication`).
2. **Install Driver**: Run `MFS100_Driver_Setup.exe` on that PC.
3. **Connect Hardware**: Plug in the Mantra MFS100 scanner.
4. **Configure Database**: In `Web.config`, ensure the `OracleDB` connection string points to your Oracle 11g server:
   ```xml
   <connectionStrings>
     <add name="OracleDB" 
          connectionString="User Id=system;Password=root;Data Source=//127.0.0.1:1521/xe;" 
          providerName="Oracle.ManagedDataAccess.Client" />
   </connectionStrings>
   ```
5. **Open & Run**:
   - Open `Authentication.sln` in Visual Studio 2015.
   - Press <kbd>Ctrl</kbd> + <kbd>F5</kbd>.
   - The status on the page will show:
     ```text
     Scanner: Connected & Ready (Mantra MFS100)
     ```

---

## 6. Common Status Codes & Troubleshooting

| Return Code | Meaning | Solution |
| :--- | :--- | :--- |
| **`0`** | **Success** | Device connected, initialized, and ready. |
| **`-999`** | **SDK DLL Not Found** | `MANTRA.MFS100.dll` is missing from `bin\`. Copy it from `Lib\` into `bin\`. |
| **`-1`** | **Device In Use or Driver Missing** | Either scanner is unplugged, driver is not installed, or `Mantra AVDM` service is holding the USB port. Stop the service and replug. |
| **`BadImageFormatException`** | **Bitness Mismatch** | A 64-bit native DLL was copied into a 32-bit IIS Express process, or vice-versa. Ensure `bin\` DLLs match the IIS Express architecture. |
| **`PInvokeStackImbalance`** | **32-bit Debugger MDA Warning** | Run in 64-bit IIS Express, or uncheck `PInvokeStackImbalance` in Visual Studio Exception Settings (<kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>E</kbd>). |
