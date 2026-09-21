using System;
using System.Net.Http;
using System.Text;
using System.Web.Script.Serialization;

namespace CentralMFA.Utils
{
    /// <summary>
    /// Lightweight .NET client for communicating with Mantra MFS100 via the local MfaScannerAgent service (Port 8034).
    /// Provides 3 clean, ready-to-call functions: CheckDevice(), CaptureFingerprint(), and CompareFingerprints().
    /// </summary>
    public class MfaScannerClient
    {
        private readonly string _agentUrl;
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        private static readonly JavaScriptSerializer _json = new JavaScriptSerializer();

        public const int MatchScoreThreshold = 1400; // Standard Mantra ISO matching threshold

        public MfaScannerClient(string agentUrl = "http://127.0.0.1:8034")
        {
            _agentUrl = string.IsNullOrWhiteSpace(agentUrl) ? "http://127.0.0.1:8034" : agentUrl.TrimEnd('/');
        }

        // =========================================================================
        // 1. INITIALIZE & CHECK DEVICE
        // =========================================================================
        /// <summary>
        /// Checks if MfaScannerAgent is running on this PC and whether the Mantra MFS100 scanner is plugged into USB.
        /// </summary>
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
                    errorMessage = "Mantra MFS100 scanner is not connected to USB.";
                    return false;
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Cannot connect to MfaScannerAgent on port 8034. Ensure MfaScannerAgent.exe is running. (" + ex.Message + ")";
                return false;
            }
        }

        // =========================================================================
        // 2. CAPTURE FINGERPRINT DATA FROM DEVICE (WITH CONFIGURABLE TIMEOUT)
        // =========================================================================
        /// <summary>
        /// Communicates with MfaScannerAgent to activate the optical scanner and wait for finger placement.
        /// The red optical light remains actively illuminated waiting for the finger to be placed firmly on the glass.
        /// </summary>
        /// <param name="timeoutSecondsOrMs">Duration the red light stays ON waiting for finger placement. Accepts seconds (e.g. 10, 15, 20, 30s) or milliseconds (e.g. 10000, 20000ms). Default: 20 seconds.</param>
        /// <param name="qualityScore">Output quality score (0 to 100).</param>
        /// <param name="errorMessage">Output error message if capture fails.</param>
        public byte[] CaptureFingerprint(int timeoutSecondsOrMs, out int qualityScore, out string errorMessage)
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
                        errorMessage = "Scanner returned an empty template.";
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
        // 3. COMPARE / MATCH TWO FINGERPRINTS
        // =========================================================================
        /// <summary>
        /// Compares the live captured template against the stored database template using the biometric algorithm.
        /// </summary>
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
