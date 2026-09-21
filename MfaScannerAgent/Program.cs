using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace MfaScannerAgent
{
    class Program
    {
        private static HttpListener _listener;
        private static bool _running = true;
        private static dynamic _device;
        private static Type _deviceType;
        private static Assembly _sdkAssembly;
        private static bool _isInitialized = false;
        private static readonly object _initLock = new object();
        private static string _deviceSerial = "";
        private static string _deviceModel = "";
        private static string _deviceMake = "";

        static void Main(string[] args)
        {
            Console.Title = "Mantra MFS100 Local Scanner Service (Port 8034)";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===============================================================");
            Console.WriteLine("   Mantra MFS100 Local Scanner Service Agent (Port 8034)      ");
            Console.WriteLine("   Provides Client-Side Scanner API for Intranet Browsers      ");
            Console.WriteLine("===============================================================");
            Console.ResetColor();

            // 1. Initial attempt to detect and initialize the Mantra MFS100 device
            EnsureDeviceInitialized();

            // 2. Start HTTP Listener on Port 8034
            StartHttpServer();
        }

        /// <summary>
        /// Ensures the Mantra MFS100 device is initialized.
        /// Supports dynamic hot-plugging: if the user plugs in the scanner after the agent started,
        /// this function automatically detects and initializes the device without requiring an agent restart.
        /// </summary>
        private static bool EnsureDeviceInitialized()
        {
            lock (_initLock)
            {
                try
                {
                    if (_device == null)
                    {
                        string appDir = AppDomain.CurrentDomain.BaseDirectory;
                        string sdkPath = Path.Combine(appDir, "MANTRA.MFS100.dll");
                        if (File.Exists(sdkPath))
                        {
                            _sdkAssembly = Assembly.LoadFrom(sdkPath);
                            _deviceType = _sdkAssembly.GetType("MANTRA.MFS100");
                            if (_deviceType != null)
                            {
                                _device = Activator.CreateInstance(_deviceType);
                            }
                        }
                    }

                    if (_device != null)
                    {
                        // Check if currently connected
                        bool isConn = false;
                        try { isConn = (bool)_device.IsConnected(); } catch { }

                        if (isConn && !_isInitialized)
                        {
                            int ret = _device.Init();
                            if (ret == 0)
                            {
                                _isInitialized = true;
                                ReadDeviceInfo();
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("[HARDWARE] Mantra MFS100 connected & initialized successfully (Code: 0)!");
                                if (!string.IsNullOrEmpty(_deviceSerial))
                                {
                                    Console.WriteLine("           Device: " + _deviceMake + " " + _deviceModel + " (Serial: " + _deviceSerial + ")");
                                }
                                Console.ResetColor();
                                return true;
                            }
                        }
                        else if (!isConn && _isInitialized)
                        {
                            // Device was unplugged
                            _isInitialized = false;
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("[HARDWARE] Mantra MFS100 was disconnected. Waiting for reconnection...");
                            Console.ResetColor();
                            return false;
                        }
                        else if (!isConn && !_isInitialized)
                        {
                            // Try an Init probe in case IsConnected requires Init first
                            int ret = _device.Init();
                            if (ret == 0)
                            {
                                _isInitialized = true;
                                ReadDeviceInfo();
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("[HARDWARE] Mantra MFS100 detected & initialized! (Serial: " + _deviceSerial + ")");
                                Console.ResetColor();
                                return true;
                            }
                        }

                        return _isInitialized;
                    }
                }
                catch (Exception ex)
                {
                    _isInitialized = false;
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("[HARDWARE] Notice during hardware probe: " + ex.Message);
                    Console.ResetColor();
                }

                return false;
            }
        }

        private static void ReadDeviceInfo()
        {
            try
            {
                if (_device != null)
                {
                    dynamic info = _device.GetDeviceInfo();
                    if (info != null)
                    {
                        _deviceSerial = info.SerialNo ?? "";
                        _deviceModel = info.Model ?? "MFS100";
                        _deviceMake = info.Make ?? "MANTRA";
                    }
                }
            }
            catch { }
        }

        private static void StartHttpServer()
        {
            _listener = new HttpListener();
            try
            {
                _listener.Prefixes.Add("http://127.0.0.1:8034/");
                _listener.Prefixes.Add("http://localhost:8034/");
                _listener.Start();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n[ONLINE] Service is listening on: http://127.0.0.1:8034/");
                Console.WriteLine("[READY] Any intranet browser page can now request fingerprint scans.");
                Console.WriteLine("Press Ctrl+C or close this window to stop.\n");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Could not bind port 8034: " + ex.Message);
                Console.ResetColor();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            while (_running)
            {
                try
                {
                    var context = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(ProcessClientRequest, context);
                }
                catch (Exception)
                {
                    if (!_running) break;
                }
            }
        }

        private static void ProcessClientRequest(object state)
        {
            HttpListenerContext context = (HttpListenerContext)state;
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            // Handle CORS Headers (allows calling from any intranet server URL)
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, Authorization");

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 200;
                response.Close();
                return;
            }

            string path = request.Url.AbsolutePath.ToLowerInvariant().TrimEnd('/');
            Console.WriteLine("[HTTP] " + DateTime.Now.ToString("HH:mm:ss") + " " + request.HttpMethod + " " + path);

            try
            {
                if (path == "" || path == "/mfs100" || path == "/mfs100/info")
                {
                    HandleInfo(response);
                }
                else if (path == "/mfs100/capture" || path == "/capture")
                {
                    HandleCapture(request, response);
                }
                else if (path == "/mfs100/simulate" || path == "/simulate")
                {
                    HandleSimulate(response);
                }
                else if (path == "/mfs100/match" || path == "/match")
                {
                    HandleMatch(request, response);
                }
                else
                {
                    SendJson(response, 404, "{\"Status\": false, \"Message\": \"Endpoint not found\"}");
                }
            }
            catch (Exception ex)
            {
                SendJson(response, 500, "{\"Status\": false, \"Message\": \"" + EscapeJson(ex.Message) + "\"}");
            }
        }

        private static void HandleInfo(HttpListenerResponse response)
        {
            bool ready = EnsureDeviceInitialized();

            string json = "{" +
                "\"Status\": true," +
                "\"Service\": \"Mantra MFS100 Local Scanner Agent\"," +
                "\"Version\": \"1.2.0\"," +
                "\"Port\": 8034," +
                "\"Device\": \"Mantra MFS100\"," +
                "\"Connected\": " + (ready ? "true" : "false") + "," +
                "\"Initialized\": " + (ready ? "true" : "false") + "," +
                "\"SerialNo\": \"" + EscapeJson(_deviceSerial) + "\"," +
                "\"Model\": \"" + EscapeJson(_deviceModel) + "\"," +
                "\"Make\": \"" + EscapeJson(_deviceMake) + "\"," +
                "\"DeviceStatus\": \"" + (ready ? "DEVICE_READY" : "DEVICE_NOT_FOUND") + "\"," +
                "\"Mode\": \"" + (ready ? "Hardware Live" : "Offline / Not Connected") + "\"" +
                "}";

            SendJson(response, 200, json);
        }

        private static void HandleCapture(HttpListenerRequest request, HttpListenerResponse response)
        {
            bool ready = EnsureDeviceInitialized();

            if (!ready)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(">>> [CAPTURE REJECTED] Mantra MFS100 device is NOT connected to USB.");
                Console.ResetColor();

                string err = "Mantra MFS100 scanner is not connected to USB. Please plug the device into your computer and try again.";
                string errJson = "{" +
                    "\"Status\": false," +
                    "\"Connected\": false," +
                    "\"IsRealHardware\": false," +
                    "\"Quality\": 0," +
                    "\"IsoTemplate\": \"\"," +
                    "\"TemplateLength\": 0," +
                    "\"ErrorMessage\": \"" + EscapeJson(err) + "\"" +
                    "}";
                SendJson(response, 400, errJson);
                return;
            }

            // Parse timeout smartly: support seconds (e.g. 10, 20, 30) or milliseconds (e.g. 10000, 20000)
            int rawTimeout = 20; // Default: 20 seconds
            if (request != null)
            {
                string tParam = request.QueryString["timeout"];
                if (string.IsNullOrEmpty(tParam) && request.HasEntityBody)
                {
                    try
                    {
                        using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                        {
                            string body = reader.ReadToEnd();
                            tParam = ExtractJsonField(body, "TimeOut");
                            if (string.IsNullOrEmpty(tParam)) tParam = ExtractJsonField(body, "timeout");
                        }
                    }
                    catch { }
                }

                if (!string.IsNullOrEmpty(tParam) && int.TryParse(tParam, out int parsed) && parsed > 0)
                {
                    rawTimeout = parsed;
                }
            }

            // If rawTimeout <= 120, treat as seconds (e.g. 10s, 20s, 30s)
            // If rawTimeout > 120, treat as milliseconds (e.g. 5000ms, 10000ms, 20000ms)
            int timeoutMs = (rawTimeout <= 120) ? (rawTimeout * 1000) : rawTimeout;

            // Enforce minimum 5 seconds and maximum 60 seconds for optical illumination
            if (timeoutMs < 5000) timeoutMs = 5000;
            if (timeoutMs > 60000) timeoutMs = 60000;

            byte[] templateBytes = null;
            int quality = 0;
            string error = null;

            int durationSec = timeoutMs / 1000;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(">>> [HARDWARE] Activating Mantra MFS100 optical sensor (red light ON for up to " + durationSec + "s)...");
            Console.WriteLine("    Please place your finger flat and firmly on the scanner glass.");
            Console.ResetColor();

            try
            {
                Type fdType = _sdkAssembly.GetType("MANTRA.FingerData");
                MethodInfo autoCap = _deviceType.GetMethod("AutoCapture");

                var sw = System.Diagnostics.Stopwatch.StartNew();

                // Loop within the timeoutMs window in case of transient finger lift or placement adjustment (-1324)
                while (sw.ElapsedMilliseconds < timeoutMs)
                {
                    int remainingMs = (int)(timeoutMs - sw.ElapsedMilliseconds);
                    if (remainingMs < 1000) remainingMs = 1000;

                    object fdObj = Activator.CreateInstance(fdType);

                    // Critical fix: IsDetectFinger MUST be false (args[3] = false)!
                    // When false, the Mantra driver turns on the red light and WAITS for the finger to be placed.
                    // When true, it checks frame 0 immediately and fails with -1324 if no finger is already touching the glass!
                    object[] args = new object[] { fdObj, remainingMs, false, false };
                    int ret = (int)autoCap.Invoke(_device, args);

                    if (ret == 0 && args[0] != null)
                    {
                        dynamic fd = args[0];
                        quality = fd.Quality;
                        templateBytes = (byte[])fd.ISOTemplate;
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine(">>> [HARDWARE SUCCESS] Biometric captured! Size: " + templateBytes.Length + " bytes, Quality: " + quality + "/100");
                        Console.ResetColor();
                        error = null;
                        break;
                    }
                    else if (ret == -1324) // Finger not placed properly or removed too quickly
                    {
                        // If we still have time remaining, keep the sensor active and allow user to place finger firmly
                        if (sw.ElapsedMilliseconds + 1500 < timeoutMs)
                        {
                            Thread.Sleep(200);
                            continue;
                        }
                        else
                        {
                            error = "Finger was not placed properly or removed too quickly. Please place finger flat and firmly on the scanner glass.";
                            break;
                        }
                    }
                    else if (ret == -1140) // Timeout
                    {
                        error = "Capture timed out after " + durationSec + " seconds. No finger was placed on the scanner.";
                        break;
                    }
                    else
                    {
                        string errMsg = _device.GetErrorMsg(ret);
                        error = "Hardware capture returned code " + ret + ": " + errMsg;
                        break;
                    }
                }

                if (templateBytes == null && string.IsNullOrEmpty(error))
                {
                    error = "Capture timed out after " + durationSec + " seconds. No finger was placed on the scanner.";
                }
            }
            catch (Exception ex)
            {
                Exception root = ex;
                while (root.InnerException != null) root = root.InnerException;
                error = root.Message;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(">>> [EXCEPTION] Hardware capture failed: " + error);
                Console.ResetColor();
            }

            if (error != null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(">>> [HARDWARE NOTICE] " + error);
                Console.ResetColor();
            }

            if (templateBytes != null && templateBytes.Length > 0)
            {
                string base64Template = Convert.ToBase64String(templateBytes);
                string json = "{" +
                    "\"Status\": true," +
                    "\"Connected\": true," +
                    "\"IsRealHardware\": true," +
                    "\"Quality\": " + quality + "," +
                    "\"IsoTemplate\": \"" + base64Template + "\"," +
                    "\"TemplateLength\": " + templateBytes.Length + "," +
                    "\"ErrorMessage\": \"\"" +
                    "}";
                SendJson(response, 200, json);
            }
            else
            {
                string json = "{" +
                    "\"Status\": false," +
                    "\"Connected\": true," +
                    "\"IsRealHardware\": true," +
                    "\"ErrorMessage\": \"" + EscapeJson(error ?? "Capture failed from optical sensor.") + "\"" +
                    "}";
                SendJson(response, 400, json);
            }
        }

        private static void HandleSimulate(HttpListenerResponse response)
        {
            byte[] templateBytes = new byte[400];
            new Random().NextBytes(templateBytes);
            int quality = 85;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(">>> [EXPLICIT DEMO SIMULATION] Generated 400-byte demo template via /simulate.");
            Console.ResetColor();

            string base64Template = Convert.ToBase64String(templateBytes);
            string json = "{" +
                "\"Status\": true," +
                "\"Connected\": false," +
                "\"IsRealHardware\": false," +
                "\"Quality\": " + quality + "," +
                "\"IsoTemplate\": \"" + base64Template + "\"," +
                "\"TemplateLength\": " + templateBytes.Length + "," +
                "\"ErrorMessage\": \"\"" +
                "}";
            SendJson(response, 200, json);
        }

        private static void HandleMatch(HttpListenerRequest request, HttpListenerResponse response)
        {
            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                body = reader.ReadToEnd();
            }

            string probeB64 = ExtractJsonField(body, "Probe");
            string galleryB64 = ExtractJsonField(body, "Gallery");

            if (string.IsNullOrEmpty(probeB64) || string.IsNullOrEmpty(galleryB64))
            {
                SendJson(response, 400, "{\"Status\": false, \"ErrorMessage\": \"Probe and Gallery templates are required.\"}");
                return;
            }

            byte[] probe = Convert.FromBase64String(probeB64);
            byte[] gallery = Convert.FromBase64String(galleryB64);

            bool ready = EnsureDeviceInitialized();
            int score = 0;
            string error = null;

            if (ready)
            {
                try
                {
                    MethodInfo matchMethod = _deviceType.GetMethod("MatchISO");
                    object[] args = new object[] { probe, gallery, 0 };
                    int ret = (int)matchMethod.Invoke(_device, args);
                    if (ret == 0)
                    {
                        score = (int)args[2];
                    }
                    else
                    {
                        error = _device.GetErrorMsg(ret);
                    }
                }
                catch (Exception ex)
                {
                    Exception root = ex;
                    while (root.InnerException != null) root = root.InnerException;
                    error = root.Message;
                }
            }
            else
            {
                // Fallback simulation match
                bool isSame = (probe.Length == gallery.Length);
                if (isSame)
                {
                    for (int i = 0; i < probe.Length; i++)
                    {
                        if (probe[i] != gallery[i]) { isSame = false; break; }
                    }
                }
                score = isSame ? 3850 : 180;
            }

            string json = "{" +
                "\"Status\": " + (error == null ? "true" : "false") + "," +
                "\"Score\": " + score + "," +
                "\"Matched\": " + (score >= 1400 ? "true" : "false") + "," +
                "\"IsRealHardware\": " + (ready ? "true" : "false") + "," +
                "\"ErrorMessage\": \"" + EscapeJson(error ?? "") + "\"" +
                "}";

            SendJson(response, 200, json);
        }

        private static string ExtractJsonField(string json, string field)
        {
            if (string.IsNullOrEmpty(json)) return "";
            string key = "\"" + field + "\"";
            int idx = json.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return "";
            int colon = json.IndexOf(':', idx + key.Length);
            if (colon < 0) return "";
            int firstQuote = json.IndexOf('"', colon + 1);
            if (firstQuote < 0) return "";
            int secondQuote = json.IndexOf('"', firstQuote + 1);
            if (secondQuote < 0) return "";
            return json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
        }

        private static void SendJson(HttpListenerResponse response, int statusCode, string json)
        {
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ");
        }
    }
}
