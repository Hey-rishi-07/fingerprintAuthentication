using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace CentralMFA.Utils
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

        private Type _deviceType;
        private Assembly _sdkAssembly;
        private readonly Dictionary<string, MethodInfo> _methodCache =
            new Dictionary<string, MethodInfo>();
        private readonly Dictionary<string, object[]> _refArgCache =
            new Dictionary<string, object[]>();

        private DeviceManager() { }

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
                        LastError = "MFS100 SDK assembly not found in bin\\.";
                        return -999;
                    }
                    Device = Activator.CreateInstance(_deviceType);
                }

                int ret = Device.Init();
                IsInitialized = (ret == 0);
                if (!IsInitialized)
                {
                    LastError = "MFS100 Init() code " + ret;
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
                ?? Type.GetType("MFS100, MFS100.Net", false);
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
                Path.Combine(appRoot, "Lib", "MANTRA.MFS100.dll")
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
        /// Compares an enrolled template (from Oracle) against a captured template (from user's browser/scanner).
        /// In demo mode, evaluates deterministic matching.
        /// </summary>
        public int MatchFingerprints(byte[] enrolledTemplate, byte[] probeTemplate, bool isDemoMode, out string error)
        {
            error = null;

            if (enrolledTemplate == null || enrolledTemplate.Length == 0)
            {
                error = "Enrolled template is missing or empty.";
                return 0;
            }
            if (probeTemplate == null || probeTemplate.Length == 0)
            {
                error = "Probe template is missing or empty.";
                return 0;
            }

            if (isDemoMode)
            {
                if (enrolledTemplate.SequenceEqual(probeTemplate))
                {
                    return 3850; // Match
                }
                return 180; // No match
            }

            if (!IsInitialized)
            {
                int initRet = InitDevice();
                if (initRet != 0)
                {
                    // If hardware initialization fails on server machine, fall back to byte comparison
                    if (enrolledTemplate.SequenceEqual(probeTemplate)) return 3850;
                    error = LastError ?? ("Matching init code: " + initRet);
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
                error = "Match Exception: " + ex.Message;
                return 0;
            }
        }

        public byte[] GenerateDemoTemplate(string seed)
        {
            byte[] template = new byte[400];
            string baseStr = string.IsNullOrWhiteSpace(seed) ? "SEED_DEMO" : seed.Trim().ToUpperInvariant();
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
            if (_refArgCache.TryGetValue(key, out args) && args != null && args.Length > parameterIndex)
            {
                return args[parameterIndex];
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
    }
}
