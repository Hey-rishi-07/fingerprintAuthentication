# Mantra MFS100 Biometric Code & Implementation Guide

This guide is designed for developers who want to understand, extract, or integrate the core **Mantra MFS100 fingerprint sensor** functionality from the **CentralMFA** project.

It provides:
1. **A ready-to-use, self-contained C# class (`Mfs100Service.cs`)** that handles the entire lifecycle (Initialization, Optical Capture, ISO Template Extraction, 1:1 Biometric Matching, and Cleanup).
2. **Line-by-line explanation** of every critical SDK method and return code.
3. **Exact pointers** to where each component lives within the CentralMFA codebase.
4. **Critical architectural guidelines** (32-bit x86 vs 64-bit, unmanaged DLL dependencies, and score thresholds).

---

## 1. Quick File Map in CentralMFA

If the other developer wants to see the actual files in this repository:

| Biometric Operation | File in CentralMFA | Key Methods / Functions |
|---|---|---|
| **Hardware Detection & USB Hot-Plug** | [MfaScannerAgent/Program.cs](file:///e:/fingerprint_scanner/MfaScannerAgent/Program.cs) | `EnsureDeviceInitialized()`, `ReadDeviceInfo()` |
| **Optical Fingerprint Capture** | [MfaScannerAgent/Program.cs](file:///e:/fingerprint_scanner/MfaScannerAgent/Program.cs) | `HandleCapture()` (calls `AutoCapture()`) |
| **1:1 Biometric Template Comparison** | [CentralMFA/Utils/DeviceManager.cs](file:///e:/fingerprint_scanner/CentralMFA/Utils/DeviceManager.cs) | `MatchFingerprints()`, calling SDK `MatchISO()` |
| **Browser Client API Communication** | [CentralMFA/Static/mfa-client.js](file:///e:/fingerprint_scanner/CentralMFA/Static/mfa-client.js) | `MfaClient.captureFingerprint()`, `MfaClient.checkStatus()` |
| **Server-Side Verification Handler** | [CentralMFA/Gateway.aspx.cs](file:///e:/fingerprint_scanner/CentralMFA/Gateway.aspx.cs) | `btnServerVerify_Click()` (fetches from Oracle and matches) |

---

## 2. Required SDK Libraries (Dependencies)

To communicate with the Mantra MFS100 scanner, the project requires **three specific DLL files** located together in the application directory or `bin/`:

1. **`MANTRA.MFS100.dll`**: The managed .NET wrapper assembly (exposes the `MFS100` and `FingerData` classes).
2. **`MFS100Dll.dll`**: The native C/C++ driver library for the optical sensor.
3. **`iengine_ansi_iso.dll`**: The biometric algorithmic engine responsible for minutiae extraction and ISO/ANSI template matching.

> [!IMPORTANT]
> **Architecture Compatibility (x86 vs x64):**
> Mantra's native `MFS100Dll.dll` and `iengine_ansi_iso.dll` are 32-bit native libraries. 
> Ensure the consuming project's **Platform Target is set to `x86`** (or if `AnyCPU`, ensure **Prefer 32-bit** is checked). If run as pure 64-bit, Windows will throw a `BadImageFormatException` or `DllNotFoundException`.

---

## 3. The Complete, Self-Contained C# Biometric Service

Give this class to the developer. It contains all 5 fundamental biometric operations in one clean, standalone C# file:

```csharp
using System;
using System.IO;
using MANTRA; // Reference to MANTRA.MFS100.dll

namespace CentralMFA.Biometrics
{
    /// <summary>
    /// Self-contained wrapper for Mantra MFS100 Fingerprint Scanner operations.
    /// Handles Initialization, Optical Capture, ISO-19794-2 Template Extraction,
    /// 1:1 Biometric Comparison, and Uninitialization.
    /// </summary>
    public class Mfs100Service : IDisposable
    {
        private MFS100 _mfs100;
        private bool _isInitialized = false;

        public bool IsInitialized => _isInitialized;

        // Recommended minimum quality for enrollment and verification (0 to 100)
        public const int DefaultQualityThreshold = 60;

        // Recommended matching score threshold for positive identification (ISO 19794-2)
        // Score >= 1400 corresponds to False Acceptance Rate (FAR) < 0.001%
        public const int MatchThreshold = 1400;

        public Mfs100Service()
        {
            _mfs100 = new MFS100();
        }

        // =====================================================================
        // 1. DEVICE INITIALIZATION
        // =====================================================================
        /// <summary>
        /// Detects and initializes the connected Mantra MFS100 USB device.
        /// </summary>
        /// <returns>True if device is detected and initialized; otherwise false.</returns>
        public bool InitializeDevice(out string errorMessage)
        {
            errorMessage = null;

            try
            {
                // Check if device is physically plugged into USB
                if (!_mfs100.IsConnected())
                {
                    errorMessage = "Mantra MFS100 device is not connected to any USB port.";
                    _isInitialized = false;
                    return false;
                }

                // Call Init() to load firmware & native calibration
                int ret = _mfs100.Init();
                if (ret == 0)
                {
                    _isInitialized = true;
                    return true;
                }
                else
                {
                    errorMessage = $"Initialization failed. Code: {ret} ({_mfs100.GetErrorMsg(ret)})";
                    _isInitialized = false;
                    return false;
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Initialization Exception: " + ex.Message;
                _isInitialized = false;
                return false;
            }
        }

        // =====================================================================
        // 2. DEVICE INFO (SERIAL NUMBER, MAKE, MODEL)
        // =====================================================================
        /// <summary>
        /// Retrieves hardware identity details from the scanner.
        /// </summary>
        public DeviceInfo GetDeviceInfo()
        {
            if (!_isInitialized && !InitializeDevice(out _))
            {
                return null;
            }
            return _mfs100.GetDeviceInfo();
        }

        // =====================================================================
        // 3. CAPTURE FINGERPRINT FROM OPTICAL SENSOR
        // =====================================================================
        /// <summary>
        /// Illuminates the optical sensor, waits for the user to place their finger,
        /// and extracts the ISO 19794-2 standard biometric template.
        /// </summary>
        /// <param name="timeoutSeconds">Maximum time to wait for finger placement (e.g. 10s).</param>
        /// <param name="qualityScore">Output quality score (0 to 100).</param>
        /// <param name="isoTemplateBytes">Output binary ISO 19794-2 template bytes.</param>
        /// <param name="bmpImageBytes">Optional output raw BMP fingerprint image.</param>
        /// <param name="errorMessage">Output error message if capture failed.</param>
        /// <returns>True if capture succeeded; otherwise false.</returns>
        public bool CaptureFingerprint(
            int timeoutSeconds,
            out int qualityScore,
            out byte[] isoTemplateBytes,
            out byte[] bmpImageBytes,
            out string errorMessage)
        {
            qualityScore = 0;
            isoTemplateBytes = null;
            bmpImageBytes = null;
            errorMessage = null;

            if (!_isInitialized)
            {
                if (!InitializeDevice(out errorMessage))
                {
                    return false;
                }
            }

            try
            {
                // Prepare output FingerData container
                FingerData fingerData = new FingerData();

                // AutoCapture parameters:
                // 1. ref FingerData: container that will receive quality, templates, and bitmap
                // 2. Timeout (ms): e.g. 10000ms = 10s
                // 3. ShowPreview (bool): false (headless / custom UI)
                // 4. IsDetectFinger (bool): true (sensor turns on and auto-triggers on contact)
                int ret = _mfs100.AutoCapture(ref fingerData, timeoutSeconds * 1000, false, true);

                if (ret == 0 && fingerData != null)
                {
                    qualityScore = fingerData.Quality;
                    isoTemplateBytes = fingerData.ISOTemplate; // Standard ISO 19794-2 bytes
                    bmpImageBytes = fingerData.FingerImage;   // Optional preview image

                    // Validate minimum acceptable quality
                    if (qualityScore < DefaultQualityThreshold)
                    {
                        errorMessage = $"Fingerprint quality ({qualityScore}%) is below minimum threshold ({DefaultQualityThreshold}%). Please place finger flat and retry.";
                        return false;
                    }

                    return true;
                }
                else
                {
                    errorMessage = $"Capture failed. Code {ret}: {_mfs100.GetErrorMsg(ret)}";
                    return false;
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Capture Exception: " + ex.Message;
                return false;
            }
        }

        // =====================================================================
        // 4. 1:1 BIOMETRIC COMPARISON (MATCHING)
        // =====================================================================
        /// <summary>
        /// Compares a freshly captured template (probe) against a registered template from the database (gallery).
        /// </summary>
        /// <param name="enrolledGalleryTemplate">The template byte array stored in Oracle / SQL Server.</param>
        /// <param name="capturedProbeTemplate">The template byte array just scanned from the sensor.</param>
        /// <param name="matchScore">Output match score computed by the biometric algorithm.</param>
        /// <param name="errorMessage">Output error details if algorithm fails.</param>
        /// <returns>True if biometric match score meets or exceeds MatchThreshold (1400).</returns>
        public bool MatchFingerprints(
            byte[] enrolledGalleryTemplate,
            byte[] capturedProbeTemplate,
            out int matchScore,
            out string errorMessage)
        {
            matchScore = 0;
            errorMessage = null;

            if (enrolledGalleryTemplate == null || enrolledGalleryTemplate.Length == 0)
            {
                errorMessage = "Enrolled gallery template is null or empty.";
                return false;
            }
            if (capturedProbeTemplate == null || capturedProbeTemplate.Length == 0)
            {
                errorMessage = "Captured probe template is null or empty.";
                return false;
            }

            if (!_isInitialized)
            {
                if (!InitializeDevice(out errorMessage))
                {
                    return false;
                }
            }

            try
            {
                int score = 0;

                // MatchISO parameters:
                // 1. byte[] GalleryTemplate: Enrolled template from database
                // 2. byte[] ProbeTemplate: Captured template from sensor
                // 3. ref int Score: Integer matching score populated by the engine
                int ret = _mfs100.MatchISO(enrolledGalleryTemplate, capturedProbeTemplate, ref score);

                if (ret == 0)
                {
                    matchScore = score;
                    // Check against standard threshold (1400)
                    return matchScore >= MatchThreshold;
                }
                else
                {
                    errorMessage = $"Biometric comparison failed. Code {ret}: {_mfs100.GetErrorMsg(ret)}";
                    return false;
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Matching Exception: " + ex.Message;
                return false;
            }
        }

        // =====================================================================
        // 5. UNINITIALIZATION & RESOURCE CLEANUP
        // =====================================================================
        /// <summary>
        /// Powers off the optical sensor and frees native handles.
        /// </summary>
        public void UninitializeDevice()
        {
            if (_isInitialized && _mfs100 != null)
            {
                try
                {
                    _mfs100.Uninit();
                }
                catch { }
                finally
                {
                    _isInitialized = false;
                }
            }
        }

        public void Dispose()
        {
            UninitializeDevice();
            _mfs100 = null;
            GC.SuppressFinalize(this);
        }
    }
}
```

---

## 4. How the Code Works: Step-by-Step Walkthrough

### Step 1: Initializing the Device (`Init`)
```csharp
MFS100 mfs = new MFS100();
if (mfs.IsConnected())
{
    int ret = mfs.Init();
    if (ret == 0) {
        // Device is ready!
    }
}
```
* `mfs.IsConnected()`: Pings the USB hub using native WinUSB to check if vendor ID `0x2c0f` / product ID `0x0100` (Mantra MFS100) is attached.
* `mfs.Init()`: Sends initialization packets to the scanner, calibrates the CMOS sensor, and loads calibration data into `iengine_ansi_iso.dll`. Returns `0` on success.

---

### Step 2: Reading Fingerprint Data from the Optical Sensor (`AutoCapture`)
```csharp
FingerData data = new FingerData();
int timeoutMs = 10000; // 10 seconds

int ret = mfs.AutoCapture(ref data, timeoutMs, false, true);
if (ret == 0)
{
    int quality = data.Quality;          // e.g. 85 (0 - 100 scale)
    byte[] isoBytes = data.ISOTemplate;  // e.g. 384-byte standard biometric template
    byte[] bmpBytes = data.FingerImage;  // Optional raw bitmap image
}
```
* When `AutoCapture` is called with `isDetectFinger = true`, the sensor turns on its red LED array.
* The hardware scans at 500 DPI. Once friction ridges are detected, the DSP assesses focus and contrast.
* Once quality stabilizes, it automatically captures the frame, extracts minutiae points (ridge endings and bifurcations), converts them into **ISO 19794-2:2005 standard minutiae format**, and returns `ret = 0`.

---

### Step 3: Storing in the Database (Enrollment)
The `ISOTemplate` byte array is what gets saved to Oracle or SQL Server:
```csharp
// Example Oracle 11g parameter binding
using (OracleCommand cmd = new OracleCommand(
    "INSERT INTO MFA_ENROLLED_USERS (PCNO, FINGERPRINT_TEMPLATE, TEMPLATE_FORMAT, QUALITY, CREATED_ON) " +
    "VALUES (:PCNO, :TEMPLATE, 'ISO19794_2', :QUALITY, SYSDATE)", conn))
{
    cmd.Parameters.Add(new OracleParameter("PCNO", "EMP001"));
    cmd.Parameters.Add(new OracleParameter("TEMPLATE", OracleDbType.Blob) { Value = isoBytes });
    cmd.Parameters.Add(new OracleParameter("QUALITY", quality));
    cmd.ExecuteNonQuery();
}
```
* **Storage format**: Use `BLOB` (in Oracle) or `VARBINARY(MAX)` (in SQL Server).
* **If passing over HTTP/JSON**: Encode with `Convert.ToBase64String(isoBytes)`.

---

### Step 4: Comparing Two Templates (`MatchISO`)
When the user wants to log in or authorize a transaction:
1. Fetch the stored byte array from the database: `byte[] galleryTemplate`.
2. Capture a fresh byte array from the scanner: `byte[] probeTemplate`.
3. Pass both to `MatchISO`:

```csharp
int matchScore = 0;
int ret = mfs.MatchISO(galleryTemplate, probeTemplate, ref matchScore);

if (ret == 0)
{
    if (matchScore >= 1400)
    {
        // MATCH SUCCESSFUL! The person on the scanner matches the enrolled record.
    }
    else
    {
        // MATCH FAILED! Different finger or impostor.
    }
}
```

#### Understanding the Match Score:
| Score Range | Meaning | Action in CentralMFA |
|---|---|---|
| **0 – 499** | Completely different finger / impostor. | Reject immediately. |
| **500 – 1399** | Partial ridge correlation, but below security threshold. | Reject (possible bad placement). |
| **1400 – 9999+** | **Positive Biometric Match**. False Accept Rate (FAR) < 0.001%. | **Approve Authentication**. |

---

## 5. How CentralMFA Implements This in a Distributed Architecture

In CentralMFA, remember that the **client browser** cannot directly talk to local USB hardware without a bridge. CentralMFA divides the workflow cleanly:

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Employee PC (Client Workstation)                         │
│    - Hardware: Physical Mantra MFS100 USB                   │
│    - Service: MfaScannerAgent.exe (Listening on Port 8034)  │
│    - Action: Runs AutoCapture(), outputs Base64 ISO template│
└──────────────────────────────┬──────────────────────────────┘
                               │ Browser POST (Port 8034)
                               ▼
┌─────────────────────────────────────────────────────────────┐
│ 2. Web Browser Page (Gateway.aspx / Enroll.aspx)            │
│    - JavaScript (mfa-client.js) receives ISO template       │
│    - Places Base64 string into hidden form field            │
│    - Submits form to CentralMFA Server                      │
└──────────────────────────────┬──────────────────────────────┘
                               │ HTTPS POST
                               ▼
┌─────────────────────────────────────────────────────────────┐
│ 3. CentralMFA Server (ASP.NET + Oracle 11g)                 │
│    - Utils/DBHelper.cs fetches gallery BLOB from Oracle     │
│    - Utils/DeviceManager.cs executes MatchISO()             │
│    - If score >= 1400: Signs HMAC token & returns to app    │
└─────────────────────────────────────────────────────────────┘
```

### Server-Side Dynamic Loading (Zero-Crash Architecture)
Notice in `CentralMFA/Utils/DeviceManager.cs`, CentralMFA loads the SDK using **Reflection**:
```csharp
MethodInfo matchMethod = _deviceType.GetMethod("MatchISO");
object[] args = new object[] { galleryTemplate, probeTemplate, 0 };
int ret = (int)matchMethod.Invoke(_device, args);
int score = (int)args[2];
```
**Why CentralMFA does this**:
If a web server is deployed in IIS without physical MFS100 hardware attached, a standard static `using MANTRA;` reference could cause IIS application pool crashes if the native C++ DLLs are missing from `System32`. The dynamic reflection method in `DeviceManager.cs` gracefully handles missing drivers with meaningful error logs instead of crashing the site.

---

## 6. How to Connect to & Use `MfaScannerAgent` (REST API & Client Code)

If the developer is building a **web application** (JavaScript/HTML) or a **client-side program** and wants to connect to the already-running `MfaScannerAgent.exe` service over local loopback (`http://127.0.0.1:8034`), here is the complete API reference and integration code:

### A. The Agent REST Endpoints

The agent listens on `http://127.0.0.1:8034` (and `http://localhost:8034`). It includes full **CORS support** (`Access-Control-Allow-Origin: *`), so any intranet web page or desktop app can call it directly.

#### 1. Device Info & Health Check: `GET /mfs100/info`
Checks if the agent is running and whether the physical Mantra MFS100 scanner is plugged into USB.

* **URL**: `http://127.0.0.1:8034/mfs100/info`
* **Response (Device Connected)**:
  ```json
  {
    "Status": true,
    "Service": "Mantra MFS100 Local Scanner Agent",
    "Version": "1.2.0",
    "Port": 8034,
    "Device": "Mantra MFS100",
    "Connected": true,
    "Initialized": true,
    "SerialNo": "1394821",
    "Model": "MFS100",
    "Make": "MANTRA",
    "DeviceStatus": "DEVICE_READY",
    "Mode": "Hardware Live"
  }
  ```
* **Response (USB Disconnected)**:
  ```json
  {
    "Status": true,
    "Connected": false,
    "Initialized": false,
    "DeviceStatus": "DEVICE_NOT_FOUND",
    "Mode": "Offline / Not Connected"
  }
  ```

---

#### 2. Trigger Optical Scan & Capture: `GET` or `POST /mfs100/capture`
Activates the red optical sensor light, waits for the user's finger, extracts the biometric template, and returns it.

* **URL**: `http://127.0.0.1:8034/mfs100/capture`
* **Response (Success - HTTP 200)**:
  ```json
  {
    "Status": true,
    "Connected": true,
    "IsRealHardware": true,
    "Quality": 84,
    "IsoTemplate": "Rk1SACAyMAAAAABmAEYAcA...",
    "TemplateLength": 384,
    "ErrorMessage": ""
  }
  ```
* **Response (Hardware Not Plugged In - HTTP 400)**:
  ```json
  {
    "Status": false,
    "Connected": false,
    "IsRealHardware": false,
    "Quality": 0,
    "IsoTemplate": "",
    "TemplateLength": 0,
    "ErrorMessage": "Mantra MFS100 scanner is not connected to USB. Please plug the device into your computer and try again."
  }
  ```

---

### B. JavaScript / Web Browser Client Code

Here is how any web page can connect to the agent to read the fingerprint:

```javascript
// 1. Check if Agent is online and USB scanner is plugged in
async function checkScanner() {
    try {
        const response = await fetch("http://127.0.0.1:8034/mfs100/info");
        const data = await response.json();
        
        if (data.Connected) {
            console.log("Scanner Ready! Serial Number:", data.SerialNo);
            return true;
        } else {
            console.warn("Agent is online, but Mantra MFS100 scanner is not plugged into USB.");
            return false;
        }
    } catch (err) {
        console.error("Scanner agent is not running on port 8034. Please start MfaScannerAgent.exe.");
        return false;
    }
}

// 2. Trigger Fingerprint Capture
async function captureFingerprint() {
    try {
        console.log("Place your finger firmly on the scanner glass (red light will illuminate)...");
        
        const response = await fetch("http://127.0.0.1:8034/mfs100/capture", {
            method: "POST",
            headers: { "Content-Type": "application/json" }
        });
        
        const result = await response.json();
        
        if (result.Status && result.IsoTemplate) {
            console.log("Fingerprint captured successfully!");
            console.log("Quality Score:", result.Quality + "/100");
            console.log("Template Length:", result.TemplateLength + " bytes");
            
            // This Base64 ISO template is what you send to your server for storage or matching:
            const templateBase64 = result.IsoTemplate;
            return templateBase64;
        } else {
            alert("Capture Failed: " + result.ErrorMessage);
            return null;
        }
    } catch (err) {
        alert("Error connecting to scanner agent: " + err.message);
        return null;
    }
}
```

---

### C. C# / Desktop Client Code (Using `HttpClient`)

If the developer is writing a desktop app or background service that connects to the agent:

```csharp
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq; // or System.Text.Json

public class MfaAgentClient
{
    private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    private const string BaseUrl = "http://127.0.0.1:8034";

    public async Task<bool> IsDeviceConnectedAsync()
    {
        try
        {
            string json = await _httpClient.GetStringAsync($"{BaseUrl}/mfs100/info");
            JObject data = JObject.Parse(json);
            return data["Connected"]?.Value<bool>() == true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<byte[]> CaptureTemplateBytesAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync($"{BaseUrl}/mfs100/capture", null);
            string json = await response.Content.ReadAsStringAsync();
            JObject data = JObject.Parse(json);

            if (data["Status"]?.Value<bool>() == true)
            {
                string b64 = data["IsoTemplate"]?.Value<string>();
                int quality = data["Quality"]?.Value<int>() ?? 0;
                Console.WriteLine($"Fingerprint captured! Quality: {quality}%");
                return Convert.FromBase64String(b64);
            }
            else
            {
                string err = data["ErrorMessage"]?.Value<string>();
                Console.WriteLine("Capture error: " + err);
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Could not connect to MfaScannerAgent: " + ex.Message);
            return null;
        }
    }
}
```

---

## 7. Checklist to Give the Other Developer

Depending on how they want to integrate:

### Path A: Direct In-Process Integration (DLLs embedded in their C# project)
1. **Copy `Mfs100Service.cs`** (from Section 3 above) into their project.
2. **Add Assembly Reference** to `MANTRA.MFS100.dll`.
3. **Copy Native DLLs**: Place `MFS100Dll.dll` and `iengine_ansi_iso.dll` in the output folder.
4. **Set Project Platform**: Change project platform from `AnyCPU` to **`x86`**.

### Path B: Web / Service Integration (Connecting to `MfaScannerAgent.exe`)
1. Run `MfaScannerAgent.exe` on the client PC (listens on `http://127.0.0.1:8034`).
2. Use the JavaScript / C# code from **Section 6** to call `GET /mfs100/info` and `POST /mfs100/capture`.
3. Send the resulting `IsoTemplate` string to the server for 1:1 matching against the database.
