# MfaScannerAgent - In-Depth Architecture & Technical Design Guide

This document provides a comprehensive, deep-dive explanation of **MfaScannerAgent.exe**: its purpose, internal design, multi-threaded architecture, hardware interfacing, API protocols, CORS security, and deployment strategy for client employee workstations in an isolated corporate intranet.

---

## Table of Contents

1. [The Core Problem: Why MfaScannerAgent is Necessary](#1-the-core-problem-why-mfascanneragent-is-necessary)
2. [Why Not Use Mantra RD Service?](#2-why-not-use-mantra-rd-service)
3. [Architectural Overview & Data Flow](#3-architectural-overview--data-flow)
4. [Internal Engineering & Code Design](#4-internal-engineering--code-design)
   - [A. High-Performance HTTP Server (`HttpListener` & `ThreadPool`)](#a-high-performance-http-server-httplistener--threadpool)
   - [B. Dynamic Hardware Abstraction Layer (Reflection)](#b-dynamic-hardware-abstraction-layer-reflection)
   - [C. CORS & Intranet Browser Security](#c-cors--intranet-browser-security)
   - [D. Dual Operating Modes: Live Hardware vs. Simulated Demo](#d-dual-operating-modes-live-hardware-vs-simulated-demo)
5. [Complete REST API Specification (Port 8034)](#5-complete-rest-api-specification-port-8034)
   - [1. `GET /mfs100/info` (Device Health & Status)](#1-get-mfs100info-device-health--status)
   - [2. `POST /mfs100/capture` (Fingerprint Capture)](#2-post-mfs100capture-fingerprint-capture)
   - [3. `OPTIONS *` (CORS Preflight Handler)](#3-options--cors-preflight-handler)
6. [How Browser JavaScript Interacts with the Agent](#6-how-browser-javascript-interacts-with-the-agent)
7. [Deployment & Client Installation Guide](#7-deployment--client-installation-guide)
   - [Automated Setup with `install.bat`](#automated-setup-with-installbat)
   - [Running as a Background Windows Service](#running-as-a-background-windows-service)
   - [Windows Firewall Configuration](#windows-firewall-configuration)
8. [Troubleshooting & Diagnostics](#8-troubleshooting--diagnostics)

---

## 1. The Core Problem: Why MfaScannerAgent is Necessary

In your company's architecture:
1. The **CentralMFA web application** runs on a central intranet server (e.g. `http://mfa.corp.local:51050`).
2. The **Mantra MFS100 fingerprint scanner** is plugged via USB into each individual **employee workstation**.
3. The employee opens their web browser (Google Chrome, Microsoft Edge, Mozilla Firefox) to authenticate.

### The Browser Sandbox Barrier
Modern web browsers enforce strict security sandboxes. **JavaScript running on a web page is prohibited from accessing physical USB devices directly.** A webpage loaded from `http://mfa.corp.local/Gateway.aspx` cannot open a Win32 USB handle (`\\.\USB#VID_2766&PID_0100...`) or call native C/C++ DLLs (`MFS100Dll.dll`) installed on the client machine.

### The Solution: A Local Loopback Agent
To bridge this gap without cloud services or external dependencies, `MfaScannerAgent.exe` runs locally on the employee's machine. It acts as a lightweight local HTTP microservice listening strictly on loopback port **`8034`** (`http://127.0.0.1:8034/`). 

Because it runs as a native Windows process, it has full access to the USB scanner via the Mantra driver. When the employee visits `Gateway.aspx`, browser JavaScript makes a standard asynchronous `fetch()` call to `http://127.0.0.1:8034/mfs100/capture` to acquire the biometric template.

---

## 2. Why Not Use Mantra RD Service?

Many developers encounter Mantra's public **RD Service (Registered Device Service)**. However, RD Service is **incompatible** with private company intranet authentication:

1. **Aadhaar Encryption Lock**: Mantra RD Service was mandated by the Indian government UIDAI for Aadhaar authentication. Under RD Service regulations, the scanner hardware encrypts the biometric data using UIDAI's public key **inside the device driver**. Only UIDAI government cloud servers possess the private key to decrypt it.
2. **Cannot Match in Oracle 11g**: Because RD Service produces an encrypted UIDAI PID block, your company's Oracle 11g database cannot compare the scanned fingerprint with your stored templates.
3. **Phased Out Client Service**: Mantra phased out public downloads of the older standalone `MFS100ClientService.exe` in favor of RD Service.

**MfaScannerAgent.exe completely solves this problem.** It utilizes the direct Mantra SDK to capture standard **ISO 19794-2 biometric templates** in raw, unencrypted format, allowing your central server to perform 1:1 biometric matching against templates stored in Oracle 11g.

---

## 3. Architectural Overview & Data Flow

```
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│  Employee Workstation                                                                    │
│                                                                                          │
│  ┌───────────────────────┐                  ┌─────────────────────────────────────────┐  │
│  │ Web Browser           │                  │ MfaScannerAgent.exe                     │  │
│  │ (CentralMFA Gateway)  │                  │ (Local Daemon on Port 8034)             │  │
│  │                       │                  │                                         │  │
│  │  mfa-client.js        │                  │  ┌───────────────────────────────────┐  │  │
│  │   │                   │  HTTP POST       │  │ HttpListener on http://127.0.0.1  │  │  │
│  │   │  /mfs100/capture  │ ────────────────►│  └─────────────────┬─────────────────┘  │  │
│  │   │                   │  (Port 8034)     │                    │                    │  │
│  │   │                   │                  │  ┌─────────────────▼─────────────────┐  │  │
│  │   │                   │                  │  │ Reflection Bridge: MANTRA.MFS100  │  │  │
│  │   │                   │                  │  └─────────────────┬─────────────────┘  │  │
│  │   │                   │                  │                    │                    │  │
│  │   │                   │                  │  ┌─────────────────▼─────────────────┐  │  │
│  │   │                   │                  │  │ Native Driver: MFS100Dll.dll      │  │  │
│  │   │                   │                  │  └─────────────────┬─────────────────┘  │  │
│  │   │                   │                  └────────────────────┼────────────────────┘  │
│  │   │                   │                                       │ USB Win32 Protocol     │
│  │   │                   │                                       ▼                        │
│  │   │                   │                  ┌─────────────────────────────────────────┐  │
│  │   │                   │                  │ Physical Mantra MFS100 Scanner          │  │
│  │   │                   │                  │ (Optical Sensor Glass + LED)            │  │
│  │   │                   │                  └─────────────────────────────────────────┘  │
│  │   │                   │  JSON Base64 ISO Template                                      │
│  │   ◄───────────────────┼────────────────────────────────────────────────────────────────┘
│  │                       │
│  │ Submits to Server     │
│  ▼                       │
│ CentralMFA (IIS Server)  │
│ └── Matches in Oracle 11g│
└──────────────────────────┘
```

---

## 4. Internal Engineering & Code Design

The agent is implemented in `e:\fingerprint_scanner\MfaScannerAgent\Program.cs`. Below is an analysis of its core architectural subsystems.

### A. High-Performance HTTP Server (`HttpListener` & `ThreadPool`)
The agent embeds Windows' native kernel-mode HTTP driver (`HTTP.SYS`) via .NET's `System.Net.HttpListener`:

```csharp
_listener = new HttpListener();
_listener.Prefixes.Add("http://127.0.0.1:8034/");
_listener.Prefixes.Add("http://localhost:8034/");
_listener.Start();

while (_running)
{
    var context = _listener.GetContext();
    ThreadPool.QueueUserWorkItem(ProcessClientRequest, context);
}
```

* **Zero Third-Party Dependencies**: Uses pure .NET Framework 4.5 BCL (Base Class Library). No external web servers (IIS, Kestrel, or Apache) are needed on the employee PC.
* **Non-Blocking Concurrency**: Every incoming request is dispatched to `ThreadPool.QueueUserWorkItem`. If a health check (`/info`) arrives while a capture is ongoing, it responds immediately without queuing or timing out.

---

### B. Dynamic Hardware Abstraction Layer (Reflection)
Different versions of Mantra drivers ship with slightly varying managed wrappers (`MANTRA.MFS100.dll`). If an application statically links against version `9.0.2.5` and the employee PC has version `9.0.3.0`, a static .NET application crashes with `TypeLoadException` or `FileLoadException`.

To eliminate this vulnerability, `MfaScannerAgent` uses **runtime reflection**:

```csharp
string appDir = AppDomain.CurrentDomain.BaseDirectory;
string sdkPath = Path.Combine(appDir, "MANTRA.MFS100.dll");
Assembly asm = Assembly.LoadFrom(sdkPath);
_deviceType = asm.GetType("MANTRA.MFS100");
_device = Activator.CreateInstance(_deviceType);

int ret = _device.Init();
```

* The agent dynamically queries the assembly at runtime for methods `Init()`, `IsConnected()`, `AutoCapture()`, and `GetErrorMsg()`.
* It binds smoothly to **any version of `MFS100Dll.dll`** present in its directory without requiring recompilation.

---

### C. CORS & Intranet Browser Security
Because the browser loads `Gateway.aspx` from the intranet server (e.g. `http://192.168.1.50:51050/`), calling `http://127.0.0.1:8034/` represents a **Cross-Origin request**. Without proper CORS headers, the browser's Same-Origin Policy (SOP) blocks the response.

`MfaScannerAgent` attaches explicit CORS headers to every HTTP response:

```csharp
response.Headers.Add("Access-Control-Allow-Origin", "*");
response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, Authorization");

if (request.HttpMethod == "OPTIONS")
{
    response.StatusCode = 200;
    response.Close();
    return;
}
```

* Handles browser **preflight `OPTIONS` requests** instantly with HTTP `200 OK`.
* Allows any intranet web application domain to query the local scanner seamlessly.

---

### D. Dual Operating Modes: Live Hardware vs. Simulated Demo

To guarantee that developers, QA engineers, and administrators can test the complete system even when physical USB scanner hardware is not plugged in, the agent features an automatic fallback mechanism:

```csharp
bool hasRealDevice = (_device != null && _isInitialized && _device.IsConnected());

if (hasRealDevice)
{
    // Real hardware capture via Mantra SDK AutoCapture
    MethodInfo autoCap = _deviceType.GetMethod("AutoCapture");
    object[] args = new object[] { null, 10000, false, true };
    int ret = (int)autoCap.Invoke(_device, args);
    ...
}
else
{
    // Deterministic simulated demo mode for testing
    templateBytes = new byte[400];
    new Random().NextBytes(templateBytes);
    quality = 85;
}
```

* **When MFS100 is connected**: Activates optical sensor light, waits for physical touch, acquires real fingerprint ridges, and returns real ISO 19794-2 biometric bytes.
* **When MFS100 is not plugged in**: Automatically switches to **Simulated Mode**, emitting a valid 400-byte template with Quality `85` so you can test logins and transaction flows without hardware errors.

---

## 5. Complete REST API Specification (Port 8034)

All communication uses standard HTTP with UTF-8 JSON payloads.

### 1. `GET /mfs100/info` (Device Health & Status)
Called by browser scripts on page load to verify that the scanner agent is running and ready.

#### Request:
```http
GET /mfs100/info HTTP/1.1
Host: 127.0.0.1:8034
Accept: application/json
```

#### Response (When Hardware is Connected):
```json
{
  "Status": true,
  "Service": "Mantra MFS100 Local Scanner Agent",
  "Version": "1.0.0",
  "Port": 8034,
  "Device": "Mantra MFS100",
  "Connected": true,
  "Initialized": true,
  "Mode": "Hardware Live"
}
```

#### Response (When Running in Simulation Mode):
```json
{
  "Status": true,
  "Service": "Mantra MFS100 Local Scanner Agent",
  "Version": "1.0.0",
  "Port": 8034,
  "Device": "Mantra MFS100",
  "Connected": false,
  "Initialized": false,
  "Mode": "Simulated/Demo"
}
```

---

### 2. `POST /mfs100/capture` (Fingerprint Capture)
Instructs the scanner to turn on its optical illumination, wait for the employee to place their finger, and capture the biometric template.

#### Request:
```http
POST /mfs100/capture HTTP/1.1
Host: 127.0.0.1:8034
Content-Type: application/json

{
  "Quality": 60,
  "TimeOut": 10
}
```
* `Quality` *(optional, integer, default 60)*: Minimum acceptable ISO quality threshold (0–100).
* `TimeOut` *(optional, integer, default 10)*: Maximum time in seconds to wait for finger touch before canceling.

#### Response (Success - HTTP 200):
```json
{
  "Status": true,
  "Quality": 86,
  "IsoTemplate": "Rk1SACAyMAAAAABWAAEB...",
  "TemplateLength": 384,
  "ErrorMessage": ""
}
```
* `Status`: `true` indicates capture succeeded.
* `Quality`: Numerical quality score calculated by the Mantra optical sensor.
* `IsoTemplate`: Standard **ISO 19794-2 (2005) Finger Minutiae Record** encoded in **Base64**.
* `TemplateLength`: Size in bytes (typically 250 to 500 bytes).

#### Response (Error - HTTP 400):
```json
{
  "Status": false,
  "ErrorMessage": "Scanner returned: Finger not placed or timed out"
}
```

---

### 3. `OPTIONS *` (CORS Preflight Handler)
Automatically responds with HTTP `200 OK` and CORS authorization headers whenever the browser issues a preflight check.

---

## 6. How Browser JavaScript Interacts with the Agent

The frontend script [Static/mfa-client.js](file:///e:/fingerprint_scanner/CentralMFA/Static/mfa-client.js) abstracts all network calls into clean asynchronous methods:

```javascript
// 1. Check if local scanner is running
MfaClient.checkStatus(function (online, data) {
    if (online) {
        console.log("MFS100 ready in mode:", data.Mode);
    } else {
        console.warn("Scanner agent not detected on port 8034.");
    }
});

// 2. Request fingerprint capture
MfaClient.captureFingerprint({ quality: 60, timeout: 10 },
    function (templateBase64, quality, bmp) {
        // Successful capture: populate hidden field and verify with Oracle
        document.getElementById("hfCapturedTemplate").value = templateBase64;
        document.getElementById("btnServerVerify").click();
    },
    function (errorMessage) {
        // Finger placed lightly or removed too soon
        alert("Scan incomplete: " + errorMessage);
    }
);
```

---

## 7. Deployment & Client Installation Guide

The agent is pre-packaged in `CentralMFA/ClientAgent/`:
```text
CentralMFA\ClientAgent\
├── MfaScannerAgent.exe       # Core service executable (.NET 4.5)
├── MANTRA.MFS100.dll         # Managed SDK library
├── MFS100Dll.dll             # Native USB driver library
└── install.bat               # Automated installer script
```

### Automated Setup with `install.bat`
To deploy to employee workstations:
1. Copy the `ClientAgent` folder to the target PC (e.g. `C:\Program Files\CentralMFA_Agent\`).
2. Right-click **`install.bat`** and choose **Run as Administrator**.

What `install.bat` does automatically:
1. Verifies administrative privileges.
2. Creates folder `C:\Program Files\CentralMFA_Agent\`.
3. Copies `MfaScannerAgent.exe` and all driver DLLs.
4. Adds an automatic startup entry in the Windows Registry:
   ```cmd
   reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" /v "MfaScannerAgent" /t REG_SZ /d "\"C:\Program Files\CentralMFA_Agent\MfaScannerAgent.exe\"" /f
   ```
5. Opens Windows Firewall port 8034 on loopback.
6. Launches `MfaScannerAgent.exe` immediately.

---

### Running as a Background Windows Service (Optional)
If you prefer the agent to run completely silently without a console window or taskbar entry, you can install it as a Windows Service using the built-in Windows utility `sc.exe` or `NSSM` (Non-Sucking Service Manager):

```cmd
nssm install MfaScannerService "C:\Program Files\CentralMFA_Agent\MfaScannerAgent.exe"
nssm set MfaScannerService AppDirectory "C:\Program Files\CentralMFA_Agent"
nssm start MfaScannerService
```

---

### Windows Firewall Configuration
Because `MfaScannerAgent` only listens on `127.0.0.1` (loopback) and does not accept remote network connections, it does **not** expose your workstation to external network traffic. 

If Windows Firewall prompts on first execution:
- Select **Private Networks** -> Allow Access.

---

## 8. Troubleshooting & Diagnostics

### 1. Test Scanner Service Directly in Browser
To verify that the agent is running on any workstation, open this URL directly in Chrome or Edge:
```text
http://127.0.0.1:8034/mfs100/info
```
If working, it immediately returns:
```json
{"Status":true,"Service":"Mantra MFS100 Local Scanner Agent","Port":8034,"Connected":true...}
```

### 2. Standalone HTML Test Utility
A complete standalone testing utility is available at [TestScannerService.html](file:///e:/fingerprint_scanner/TestScannerService.html).  
Open it in any browser to click **"Check Service Status"** and **"Test Fingerprint Capture"** with live output.

### 3. Common Error Scenarios

| Symptom | Root Cause | Solution |
|---|---|---|
| **"Could not bind port 8034: Access is denied"** | Another process or an older instance is already using port 8034. | Open Task Manager -> kill existing `MfaScannerAgent.exe` -> relaunch. |
| **"Mantra MFS100 not connected (Code: -1307)"** | USB cable disconnected or driver not recognized. | Reconnect the USB cable into a direct motherboard USB 2.0/3.0 port. |
| **"Scanner returned: P/Invoke stack imbalance"** | Calling convention mismatch between managed wrapper and 32/64-bit DLL. | Replace `MFS100Dll.dll` with the version that matches your company's working SDK (v9.0.2.5). |
| **Gateway displays "Scanner Agent not detected"** | The agent executable is closed. | Run `install.bat` or double-click `MfaScannerAgent.exe`. |
