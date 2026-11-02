using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace FingerprintVerifyPOC
{
    /// <summary>
    /// Thin reflection wrapper around the Mantra MFS100 .NET SDK (MANTRA.MFS100.dll).
    /// Reflection is used so the program works with whichever SDK build is installed
    /// and does not need a compile-time reference to it.
    ///
    /// The MFS100 SDK has no dedicated "LED on/off" API: the sensor light turns ON
    /// while a capture is running and OFF when it stops. The "blink" is therefore
    /// produced by quickly starting and stopping a capture a few times.
    /// </summary>
    public sealed class Scanner : IDisposable
    {
        public const int ErrTimeout = -1140;
        public const int ErrFingerNotProper = -1324;

        private readonly object _sync = new object();
        private Assembly _sdk;
        private Type _deviceType;
        private Type _fingerDataType;
        private object _device;
        private bool _initialized;
        private bool _handlerAttached;

        public string LastError { get; private set; }
        public string SerialNo { get; private set; }
        public string Model { get; private set; }

        public bool IsReady
        {
            get { lock (_sync) { return _initialized && IsConnectedInternal(); } }
        }

        /// <summary>Loads the SDK (once) and initialises the device. Safe to call repeatedly (hot-plug).</summary>
        public bool EnsureInitialized()
        {
            lock (_sync)
            {
                LastError = null;
                try
                {
                    if (_device == null)
                    {
                        string sdkPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MANTRA.MFS100.dll");
                        if (!File.Exists(sdkPath))
                        {
                            LastError = "MANTRA.MFS100.dll not found next to the program (" + sdkPath + ").";
                            return false;
                        }
                        _sdk = Assembly.LoadFrom(sdkPath);
                        _deviceType = _sdk.GetType("MANTRA.MFS100");
                        _fingerDataType = _sdk.GetType("MANTRA.FingerData");
                        if (_deviceType == null || _fingerDataType == null)
                        {
                            LastError = "MANTRA.MFS100 / MANTRA.FingerData types not found in SDK assembly.";
                            return false;
                        }
                        _device = Activator.CreateInstance(_deviceType);
                        AttachNoOpCaptureHandler();
                    }

                    if (_initialized && IsConnectedInternal()) return true;

                    // Device was unplugged or never initialised: (re)initialise it.
                    _initialized = false;
                    int ret = (int)Call("Init");
                    if (ret != 0)
                    {
                        LastError = "Scanner Init() failed (" + ret + "): " + ErrorMessage(ret) +
                                    ". Check the USB cable, the MFS100 driver, and that no other program/RD service is using the scanner.";
                        return false;
                    }
                    _initialized = true;
                    ReadDeviceInfo();
                    return true;
                }
                catch (Exception ex)
                {
                    _initialized = false;
                    LastError = Root(ex).Message;
                    return false;
                }
            }
        }

        /// <summary>Blinks the sensor light by briefly starting/stopping capture. Never throws.</summary>
        public void Blink(int count, int onMs, int offMs, int settleMs)
        {
            if (count <= 0) return;
            lock (_sync)
            {
                MethodInfo start = FindMethod("StartCapture", -1);
                MethodInfo stop = FindMethod("StopCapture", 0);
                if (start == null || stop == null) return;

                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        start.Invoke(_device, BuildStartCaptureArgs(start, onMs + 2000));
                        Thread.Sleep(onMs);
                    }
                    catch { }
                    try { stop.Invoke(_device, null); } catch { }
                    Thread.Sleep(offMs);
                }
                // make sure the blink capture is fully stopped before the real capture starts
                try { stop.Invoke(_device, null); } catch { }
                Thread.Sleep(Math.Max(0, settleMs));
            }
        }

        /// <summary>
        /// Turns the light on and waits up to <paramref name="timeoutMs"/> for a finger.
        /// Returns 0 on success, otherwise an SDK error code (e.g. -1140 timeout).
        /// </summary>
        public int Capture(int timeoutMs, int minQuality, Action<string> notice,
                           out byte[] isoTemplate, out byte[] ansiTemplate, out int quality)
        {
            isoTemplate = null;
            ansiTemplate = null;
            quality = 0;

            lock (_sync)
            {
                MethodInfo autoCap = FindMethod("AutoCapture", 4);
                if (autoCap == null)
                {
                    LastError = "AutoCapture method not found in SDK.";
                    return -1;
                }

                var sw = System.Diagnostics.Stopwatch.StartNew();
                int lastRet = ErrTimeout;

                while (sw.ElapsedMilliseconds < timeoutMs)
                {
                    int remaining = (int)(timeoutMs - sw.ElapsedMilliseconds);
                    if (remaining < 1000) remaining = 1000;

                    object fd = Activator.CreateInstance(_fingerDataType);
                    // isDetectFinger MUST be false: the driver then switches the light on and
                    // waits for a finger, instead of failing immediately when the glass is empty.
                    object[] args = new object[] { fd, remaining, false, false };
                    int ret;
                    try
                    {
                        ret = (int)autoCap.Invoke(_device, args);
                    }
                    catch (Exception ex)
                    {
                        LastError = Root(ex).Message;
                        return -1;
                    }
                    lastRet = ret;

                    if (ret == 0 && args[0] != null)
                    {
                        int q = Convert.ToInt32(GetProp(args[0], "Quality") ?? 0);
                        if (q < minQuality && sw.ElapsedMilliseconds + 1500 < timeoutMs)
                        {
                            if (notice != null) notice("Low quality (" + q + "). Lift and place the finger again, flat and firm.");
                            Thread.Sleep(300);
                            continue;
                        }
                        quality = q;
                        isoTemplate = GetProp(args[0], "ISOTemplate") as byte[];
                        ansiTemplate = GetProp(args[0], "ANSITemplate") as byte[];
                        return 0;
                    }

                    if (ret == ErrFingerNotProper && sw.ElapsedMilliseconds + 1500 < timeoutMs)
                    {
                        // finger lifted too early / not placed properly -> keep waiting in the same window
                        Thread.Sleep(200);
                        continue;
                    }

                    LastError = ErrorMessage(ret);
                    return ret;
                }

                if (lastRet == 0) lastRet = ErrTimeout;
                LastError = ErrorMessage(lastRet);
                return lastRet == ErrFingerNotProper ? ErrFingerNotProper : ErrTimeout;
            }
        }

        /// <summary>Matches two templates with MatchISO or MatchANSI. Returns the score, or -1 on error.</summary>
        public int Match(byte[] probe, byte[] gallery, bool ansi, out string error)
        {
            error = null;
            lock (_sync)
            {
                MethodInfo m = FindMethod(ansi ? "MatchANSI" : "MatchISO", 3);
                if (m == null)
                {
                    error = (ansi ? "MatchANSI" : "MatchISO") + " method not found in SDK.";
                    return -1;
                }
                try
                {
                    object[] args = new object[] { probe, gallery, 0 };
                    int ret = (int)m.Invoke(_device, args);
                    if (ret != 0)
                    {
                        error = "Match returned " + ret + ": " + ErrorMessage(ret);
                        return -1;
                    }
                    return Convert.ToInt32(args[2]);
                }
                catch (Exception ex)
                {
                    error = Root(ex).Message;
                    return -1;
                }
            }
        }

        public string ErrorMessage(int code)
        {
            if (code == ErrTimeout) return "Timed out - no finger was placed on the scanner";
            if (code == ErrFingerNotProper) return "Finger not placed properly (lifted too early or partial)";
            try
            {
                MethodInfo m = FindMethod("GetErrorMsg", 1);
                if (m != null && _device != null) return Convert.ToString(m.Invoke(_device, new object[] { code }));
            }
            catch { }
            return "Error " + code;
        }

        public void Dispose()
        {
            lock (_sync)
            {
                try { if (_device != null && _initialized) Call("Uninit"); } catch { }
                _initialized = false;
            }
        }

        // ------------------------------------------------------------------ helpers

        private bool IsConnectedInternal()
        {
            try
            {
                MethodInfo m = FindMethod("IsConnected", 0);
                return m == null || (bool)m.Invoke(_device, null);
            }
            catch { return false; }
        }

        private void ReadDeviceInfo()
        {
            try
            {
                object info = Call("GetDeviceInfo");
                if (info == null) return;
                SerialNo = Convert.ToString(GetProp(info, "SerialNo"));
                Model = Convert.ToString(GetProp(info, "Model"));
            }
            catch { }
        }

        /// <summary>
        /// StartCapture raises OnCaptureCompleted when it ends. Some SDK builds crash if nobody
        /// is subscribed, so we attach a do-nothing handler (built dynamically for the SDK's delegate type).
        /// </summary>
        private void AttachNoOpCaptureHandler()
        {
            if (_handlerAttached) return;
            try
            {
                EventInfo ev = _deviceType.GetEvent("OnCaptureCompleted");
                if (ev == null) return;
                Type handlerType = ev.EventHandlerType;
                MethodInfo invoke = handlerType.GetMethod("Invoke");
                ParameterExpression[] ps = invoke.GetParameters()
                    .Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToArray();
                Expression body = invoke.ReturnType == typeof(void)
                    ? (Expression)Expression.Empty()
                    : Expression.Default(invoke.ReturnType);
                Delegate noop = Expression.Lambda(handlerType, body, ps).Compile();
                ev.AddEventHandler(_device, noop);
                _handlerAttached = true;
            }
            catch { }
        }

        private static object[] BuildStartCaptureArgs(MethodInfo start, int timeoutMs)
        {
            ParameterInfo[] ps = start.GetParameters();
            object[] args = new object[ps.Length];
            int intIndex = 0;
            for (int i = 0; i < ps.Length; i++)
            {
                Type t = ps[i].ParameterType;
                if (t == typeof(int))
                {
                    // StartCapture(int quality, int timeout, bool preview)
                    args[i] = (intIndex++ == 0) ? 40 : timeoutMs;
                }
                else if (t == typeof(bool)) args[i] = false;
                else args[i] = t.IsValueType ? Activator.CreateInstance(t) : null;
            }
            return args;
        }

        private object Call(string name)
        {
            MethodInfo m = FindMethod(name, 0);
            if (m == null) throw new MissingMethodException("MANTRA.MFS100", name);
            return m.Invoke(_device, null);
        }

        /// <param name="paramCount">-1 = any overload</param>
        private MethodInfo FindMethod(string name, int paramCount)
        {
            if (_deviceType == null) return null;
            return _deviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == name && (paramCount < 0 || m.GetParameters().Length == paramCount))
                .OrderBy(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        private static object GetProp(object obj, string name)
        {
            if (obj == null) return null;
            Type t = obj.GetType();
            PropertyInfo p = t.GetProperty(name);
            if (p != null) return p.GetValue(obj, null);
            FieldInfo f = t.GetField(name);
            return f != null ? f.GetValue(obj) : null;
        }

        private static Exception Root(Exception ex)
        {
            while (ex.InnerException != null) ex = ex.InnerException;
            return ex;
        }
    }
}
