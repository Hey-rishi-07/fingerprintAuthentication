# Mantra MFS100 .NET (C#) Biometric Integration Guide

This guide is for .NET (C#) developers who want to integrate **Mantra MFS100 fingerprint scanner** operations using the **MFA Scanner Agent** (`http://127.0.0.1:8034`) running on the user's PC.

It provides a **single, copy-pasteable C# class (`MfaScannerClient.cs`)** containing the 3 core functions:
1. **`CheckDevice()`**: Initializes and verifies that the scanner hardware is connected.
2. **`CaptureFingerprint()`**: Captures the live fingerprint via `MfaScannerAgent` and returns the ISO-19794-2 template.
3. **`CompareFingerprints()`**: Compares the live fingerprint against the stored database record.

---

## 1. The Complete C# Class (`MfaScannerClient.cs`)

Copy and paste this class directly into your .NET project. It has **zero external NuGet dependencies** (uses standard .NET `HttpClient` and built-in JSON deserialization):

```csharp
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Web.Script.Serialization; // In System.Web.Extensions.dll (or replace with any JSON parser)

namespace CentralMFA.Client
{
    /// <summary>
    /// Client helper to interact with Mantra MFS100 via the local MfaScannerAgent service (Port 8034).
    /// </summary>
    public class MfaScannerClient
    {
        // Port 8034 loopback address where MfaScannerAgent runs on the user's PC
        private readonly string _agentUrl;
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        private static readonly JavaScriptSerializer _json = new JavaScriptSerializer();

        public const int MatchScoreThreshold = 1400; // Standard Mantra ISO matching threshold

        public MfaScannerClient(string agentUrl = "http://127.0.0.1:8034")
        {
            _agentUrl = agentUrl.TrimEnd('/');
        }

        // =========================================================================
        // FUNCTION 1: INITIALIZE & CHECK DEVICE
        // =========================================================================
        /// <summary>
        /// Checks if the MfaScannerAgent is running on this PC and whether the
        /// physical Mantra MFS100 USB scanner is plugged in and initialized.
        /// </summary>
        /// <param name="serialNo">Outputs scanner serial number if connected.</param>
        /// <param name="errorMessage">Outputs error details if check fails.</param>
        /// <returns>True if device is physically plugged in and ready; otherwise false.</returns>
        public bool CheckDevice(out string serialNo, out string errorMessage)
        {
            serialNo = string.Empty;
            errorMessage = string.Empty;

            try
            {
                var response = _http.GetAsync($"{_agentUrl}/mfs100/info").GetAwaiter().GetResult();
                string jsonString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                var result = _json.Deserialize<dynamic>(jsonString);
                bool connected = result.ContainsKey("Connected") && Convert.ToBoolean(result["Connected"]);

                if (connected)
                {
                    serialNo = result.ContainsKey("SerialNo") ? Convert.ToString(result["SerialNo"]) : "";
                    return true;
                }
                else
                {
                    errorMessage = "Mantra MFS100 scanner is not plugged into USB.";
                    return false;
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Cannot connect to MfaScannerAgent on port 8034. Please ensure MfaScannerAgent.exe is running. (" + ex.Message + ")";
                return false;
            }
        }

        // =========================================================================
        // FUNCTION 2: CAPTURE FINGERPRINT DATA FROM DEVICE (WITH CONFIGURABLE TIMEOUT)
        // =========================================================================
        /// <summary>
        /// Communicates with MfaScannerAgent to turn on the optical scanner, wait for finger
        /// placement, and return the extracted standard ISO-19794-2 biometric template.
        /// </summary>
        /// <param name="timeoutSeconds">Number of seconds the red light stays ON waiting for finger placement (e.g. 15, 20, 30s).</param>
        /// <param name="qualityScore">Outputs the capture quality (0 to 100).</param>
        /// <param name="errorMessage">Outputs error message if capture fails.</param>
        /// <returns>Binary byte array of the ISO template, or null if capture failed.</returns>
        public byte[] CaptureFingerprint(int timeoutSeconds, out int qualityScore, out string errorMessage)
        {
            qualityScore = 0;
            errorMessage = string.Empty;

            try
            {
                // Send timeout parameter in the URL so the optical red light stays on for the specified seconds
                string url = $"{_agentUrl}/mfs100/capture?timeout={timeoutSeconds}";
                var response = _http.PostAsync(url, null).GetAwaiter().GetResult();
                string jsonString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                var result = _json.Deserialize<dynamic>(jsonString);
                bool status = result.ContainsKey("Status") && Convert.ToBoolean(result["Status"]);

                if (status)
                {
                    qualityScore = result.ContainsKey("Quality") ? Convert.ToInt32(result["Quality"]) : 0;
                    string base64Template = Convert.ToString(result["IsoTemplate"]);

                    if (!string.IsNullOrEmpty(base64Template))
                    {
                        return Convert.FromBase64String(base64Template);
                    }
                    else
                    {
                        errorMessage = "Scanner returned an empty biometric template.";
                        return null;
                    }
                }
                else
                {
                    errorMessage = result.ContainsKey("ErrorMessage")
                        ? Convert.ToString(result["ErrorMessage"])
                        : "Fingerprint capture failed.";
                    return null;
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Failed to communicate with scanner agent: " + ex.Message;
                return null;
            }
        }

        /// <summary>
        /// Overload with default 20-second illumination timeout.
        /// </summary>
        public byte[] CaptureFingerprint(out int qualityScore, out string errorMessage)
        {
            return CaptureFingerprint(20, out qualityScore, out errorMessage); // Default: 20 seconds
        }

        // =========================================================================
        // FUNCTION 3: COMPARE / MATCH TWO FINGERPRINTS
        // =========================================================================
        /// <summary>
        /// Compares the newly captured fingerprint template against the stored template
        /// from the database using the biometric matching engine.
        /// </summary>
        /// <param name="storedTemplate">The enrolled template bytes fetched from the database (Oracle/SQL).</param>
        /// <param name="capturedTemplate">The live template bytes scanned from the device.</param>
        /// <param name="matchScore">Outputs the biometric matching score.</param>
        /// <param name="errorMessage">Outputs error message if comparison fails.</param>
        /// <returns>True if match score >= 1400 (confirmed match); otherwise false.</returns>
        public bool CompareFingerprints(byte[] storedTemplate, byte[] capturedTemplate, out int matchScore, out string errorMessage)
        {
            matchScore = 0;
            errorMessage = string.Empty;

            if (storedTemplate == null || storedTemplate.Length == 0)
            {
                errorMessage = "Stored gallery template is null or empty.";
                return false;
            }
            if (capturedTemplate == null || capturedTemplate.Length == 0)
            {
                errorMessage = "Captured probe template is null or empty.";
                return false;
            }

            try
            {
                // Prepare JSON payload for the agent's match endpoint
                string payload = _json.Serialize(new
                {
                    Probe = Convert.ToBase64String(capturedTemplate),
                    Gallery = Convert.ToBase64String(storedTemplate)
                });

                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var response = _http.PostAsync($"{_agentUrl}/mfs100/match", content).GetAwaiter().GetResult();
                string jsonString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                var result = _json.Deserialize<dynamic>(jsonString);
                bool status = result.ContainsKey("Status") && Convert.ToBoolean(result["Status"]);

                if (status)
                {
                    matchScore = result.ContainsKey("Score") ? Convert.ToInt32(result["Score"]) : 0;
                    
                    // Match score >= 1400 corresponds to standard Mantra ISO positive identification (FAR < 0.001%)
                    return matchScore >= MatchScoreThreshold;
                }
                else
                {
                    errorMessage = result.ContainsKey("ErrorMessage") ? Convert.ToString(result["ErrorMessage"]) : "Comparison failed.";
                    return false;
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Matching error: " + ex.Message;
                return false;
            }
        }
    }
}
```

---

## 2. How to Call These Functions in Your Application

Here is an example showing how simple it is to use in any .NET application:

```csharp
using System;
using CentralMFA.Client;

public class Program
{
    public static void Main()
    {
        var scannerClient = new MfaScannerClient();

        // -------------------------------------------------------------
        // STEP 1: INITIALIZE & CHECK HARDWARE
        // -------------------------------------------------------------
        Console.WriteLine("1. Checking scanner status...");
        if (!scannerClient.CheckDevice(out string serialNo, out string err))
        {
            Console.WriteLine("Error: " + err);
            return;
        }
        Console.WriteLine("Mantra MFS100 Connected! S/N: " + serialNo);

        // -------------------------------------------------------------
        // STEP 2: CAPTURE FINGERPRINT (Red light on, place finger)
        // -------------------------------------------------------------
        Console.WriteLine("\n2. Place your finger firmly on the scanner glass...");
        byte[] liveTemplate = scannerClient.CaptureFingerprint(out int quality, out string capErr);

        if (liveTemplate == null)
        {
            Console.WriteLine("Capture failed: " + capErr);
            return;
        }
        Console.WriteLine($"Captured successfully! Quality: {quality}%, Size: {liveTemplate.Length} bytes");

        // -------------------------------------------------------------
        // STEP 3: COMPARE WITH STORED TEMPLATE (From Oracle / SQL Server)
        // -------------------------------------------------------------
        Console.WriteLine("\n3. Comparing with registered employee record in database...");
        byte[] storedTemplateFromDatabase = GetEnrolledTemplateFromDatabase("EMP001");

        bool isVerified = scannerClient.CompareFingerprints(
            storedTemplateFromDatabase, 
            liveTemplate, 
            out int matchScore, 
            out string matchErr
        );

        if (isVerified)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n>>> BIOMETRIC MATCH APPROVED! Match Score: {matchScore} (Threshold: 1400)");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n>>> ACCESS DENIED! Fingerprint does not match. Score: {matchScore}");
            if (!string.IsNullOrEmpty(matchErr)) Console.WriteLine("Details: " + matchErr);
            Console.ResetColor();
        }
    }

    private static byte[] GetEnrolledTemplateFromDatabase(string pcno)
    {
        // Replace with your ADO.NET / Oracle / SQL Server query
        // SELECT FINGERPRINT_TEMPLATE FROM MFA_ENROLLED_USERS WHERE PCNO = :pcno
        return new byte[384]; 
    }
}
```

---

## 3. Method Summary Table

| Method Name | Parameters | Returns | Purpose |
|---|---|---|---|
| **`CheckDevice`** | `out string serialNo, out string errorMessage` | `bool` | Pings agent port 8034, detects USB scanner, verifies calibration. |
| **`CaptureFingerprint`** | `out int qualityScore, out string errorMessage` | `byte[]` | Lights up optical sensor, detects finger touch, returns standard ISO 19794-2 template bytes. |
| **`CompareFingerprints`** | `byte[] stored, byte[] live, out int score, out string error` | `bool` | Matches live template against database record. Returns `true` if `score >= 1400`. |

---

## 4. What is Required on the User's PC

* The physical **Mantra MFS100** scanner plugged into a USB port.
* The official **Mantra MFS100 Driver** installed.
* **`MfaScannerAgent.exe`** running on the user's PC (listening on port 8034).
