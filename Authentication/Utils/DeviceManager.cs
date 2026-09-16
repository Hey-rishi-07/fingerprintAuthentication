using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace Authentication.Utils
{
    public sealed class DeviceManager
    {
        private static readonly Lazy<DeviceManager> _instance =
            new Lazy<DeviceManager>(() => new DeviceManager());
        public static DeviceManager Instance { get { return _instance.Value; } }

        public dynamic Device { get; private set; }
        public bool IsInitialized { get; private set; }
        public string LastError { get; private set; }

        public const int MatchThreshold = 1400;
        public const int CaptureTimeoutMs = 10000;

        private Type _deviceType;
        private Assembly _sdkAssembly;
        private readonly Dictionary<string, MethodInfo> _methodCache =
            new Dictionary<string, MethodInfo>();
        private readonly Dictionary<string, object[]> _refArgCache =
            new Dictionary<string, object[]>();

        private DeviceManager() { }

        /// <summary>
        /// Attempts to initialize the physical MFS100 device.
        /// </summary>
        public int InitDevice()
        {
            LastError = null;
            try
            {
                if (Device == null)
                {
                    _deviceType = ResolveMfs100Type();
                    if (_deviceType == null)
                    {
                        LastError = "MFS100 SDK assembly not found. Ensure MANTRA.MFS100.dll is in bin\\.";
                        return -999;
                    }

                    Device = Activator.CreateInstance(_deviceType);
                }

                int ret = Device.Init();
                IsInitialized = (ret == 0);
                if (!IsInitialized)
                {
                    LastError = "MFS100 Init() failed with code " + ret + ". Ensure the device is connected via USB and drivers are installed.";
                }
                return ret;
            }
            catch (Exception ex)
            {
                IsInitialized = false;
                Exception root = ex;
                while (root.InnerException != null) root = root.InnerException;
                LastError = root.GetType().Name + ": " + root.Message;
                return -1;
            }
        }

        private Type ResolveMfs100Type()
        {
            Type t = Type.GetType("MANTRA.MFS100, MANTRA.MFS100", false)
                ?? Type.GetType("MANTRA.MFS100, MANTRA.MFS100.Net", false)
                ?? Type.GetType("MFS100, MFS100.Net", false)
                ?? Type.GetType("Mantra.MFS100.MFS100, Mantra.MFS100", false);
            if (t != null) return t;

            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name.IndexOf("MFS100", StringComparison.OrdinalIgnoreCase) >= 0);
            if (asm != null)
            {
                _sdkAssembly = asm;
                return asm.GetTypes().FirstOrDefault(x => x.Name == "MFS100");
            }

            string binDir = HttpRuntime.BinDirectory;
            string appRoot = AppDomain.CurrentDomain.BaseDirectory;
            string[] searchPaths = new string[]
            {
                Path.Combine(binDir, "MANTRA.MFS100.dll"),
                Path.Combine(binDir, "MFS100.Net.dll"),
                Path.Combine(appRoot, "Lib", "MANTRA.MFS100.dll"),
                @"C:\Program Files\Mantra\MFS100 SDK\SDK\Dotnet\MANTRA.MFS100.dll",
                @"C:\Program Files (x86)\Mantra\MFS100 SDK\SDK\Dotnet\MANTRA.MFS100.dll"
            };

            foreach (string path in searchPaths)
            {
                if (!File.Exists(path)) continue;
                try
                {
                    _sdkAssembly = Assembly.LoadFrom(path);
                    t = _sdkAssembly.GetType("MANTRA.MFS100")
                        ?? _sdkAssembly.GetTypes().FirstOrDefault(x => x.Name == "MFS100");
                    if (t != null) return t;
                }
                catch { }
            }

            return null;
        }

        /// <summary>
        /// Captures fingerprint template from MFS100, or generates a mock template in demo mode.
        /// </summary>
        public byte[] CaptureFingerprint(bool isDemoMode, string demoPcNoSeed, out int quality, out string error)
        {
            quality = 0;
            error = null;

            if (isDemoMode)
            {
                quality = 85;
                // Generate deterministic 400-byte demo template based on PCNO seed
                return GenerateDemoTemplate(demoPcNoSeed);
            }

            if (!IsInitialized)
            {
                int initRet = InitDevice();
                if (initRet != 0)
                {
                    error = LastError ?? ("Scanner init failed with code " + initRet);
                    return null;
                }
            }

            try
            {
                bool connected = Device.IsConnected();
                if (!connected)
                {
                    error = "MFS100 scanner is not connected. Please plug in the USB cable.";
                    return null;
                }

                // Call AutoCapture(callback, timeout, detectFinger, ...)
                int ret = InvokeMethod<int>("AutoCapture",
                    new object[] { null, CaptureTimeoutMs, false, true },
                    new int[] { 0 });

                if (ret != 0)
                {
                    string errMsg = Device.GetErrorMsg(ret);
                    error = "Capture failed: " + errMsg;
                    return null;
                }

                object fingerData = GetRefArg("AutoCapture", 0);
                if (fingerData == null)
                {
                    error = "No fingerprint data returned by scanner.";
                    return null;
                }

                dynamic fd = fingerData;
                quality = fd.Quality;

                if (quality < 40)
                {
                    error = "Poor fingerprint quality (" + quality + "/100). Please place your finger firmly and retry.";
                    return null;
                }

                byte[] template = (byte[])fd.ISOTemplate;
                if (template == null || template.Length == 0)
                {
                    error = "ISOTemplate is empty.";
                    return null;
                }

                return template;
            }
            catch (Exception ex)
            {
                error = "Scanner Capture Error: " + ex.Message;
                return null;
            }
        }

        /// <summary>
        /// Matches a probe template against an enrolled template using Mantra's MatchISO algorithm.
        /// In demo mode, evaluates deterministic matching based on content equality.
        /// </summary>
        public int MatchFingerprints(byte[] enrolledTemplate, byte[] probeTemplate, bool isDemoMode, out string error)
        {
            error = null;

            if (enrolledTemplate == null || enrolledTemplate.Length == 0)
            {
                error = "Enrolled template is null or empty.";
                return 0;
            }
            if (probeTemplate == null || probeTemplate.Length == 0)
            {
                error = "Probe template is null or empty.";
                return 0;
            }

            if (isDemoMode)
            {
                // In demo mode, compare byte arrays
                if (enrolledTemplate.SequenceEqual(probeTemplate))
                {
                    return 3850; // High match score
                }
                return 180; // Low score (no match)
            }

            if (!IsInitialized)
            {
                int initRet = InitDevice();
                if (initRet != 0)
                {
                    error = LastError ?? ("Scanner init failed with code " + initRet);
                    return 0;
                }
            }

            try
            {
                int score = 0;
                int ret = InvokeMethod<int>("MatchISO",
                    new object[] { enrolledTemplate, probeTemplate, 0 },
                    new int[] { 2 });

                if (ret == 0)
                {
                    object refVal = GetRefArg("MatchISO", 2);
                    if (refVal != null)
                    {
                        score = Convert.ToInt32(refVal);
                    }
                    return score;
                }
                else
                {
                    string msg = Device.GetErrorMsg(ret);
                    error = "MatchISO failed: " + msg;
                    return 0;
                }
            }
            catch (Exception ex)
            {
                error = "Matching Error: " + ex.Message;
                return 0;
            }
        }

        public T InvokeMethod<T>(string methodName, object[] parameters, int[] refParamIndices)
        {
            if (_deviceType == null) return default(T);

            MethodInfo method;
            if (!_methodCache.TryGetValue(methodName, out method))
            {
                method = _deviceType.GetMethod(methodName);
                _methodCache[methodName] = method;
            }

            if (method == null) return default(T);

            object result = method.Invoke(Device, parameters);

            if (refParamIndices != null && refParamIndices.Length > 0)
            {
                string key = methodName + "_" + string.Join("_", refParamIndices);
                _refArgCache[key] = parameters;
            }

            return (T)Convert.ChangeType(result, typeof(T));
        }

        public object GetRefArg(string methodName, int parameterIndex)
        {
            string key = methodName + "_" + parameterIndex;
            object[] args;
            if (_refArgCache.TryGetValue(key, out args)
                && args != null && args.Length > parameterIndex)
            {
                return args[parameterIndex];
            }

            foreach (var kvp in _refArgCache)
            {
                if (kvp.Key.StartsWith(methodName + "_")
                    && kvp.Value != null
                    && kvp.Value.Length > parameterIndex)
                {
                    return kvp.Value[parameterIndex];
                }
            }

            return null;
        }

        public void UninitDevice()
        {
            try
            {
                if (Device != null)
                {
                    Device.Uninit();
                    Device.Dispose();
                    Device = null;
                }
            }
            catch { }
            IsInitialized = false;
            _methodCache.Clear();
            _refArgCache.Clear();
        }

        /// <summary>
        /// Generates a reproducible 400-byte demo template based on the PCNO string.
        /// </summary>
        private static byte[] GenerateDemoTemplate(string seed)
        {
            byte[] template = new byte[400];
            string baseStr = string.IsNullOrWhiteSpace(seed) ? "DEFAULT_SEED" : seed.Trim().ToUpper();
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(baseStr));
                for (int i = 0; i < 400; i++)
                {
                    template[i] = (byte)(hash[i % hash.Length] ^ (i & 0xFF));
                }
            }
            return template;
        }
    }
}
