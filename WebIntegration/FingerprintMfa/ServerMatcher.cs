using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Web;
using System.Web.Hosting;

namespace FingerprintMfa
{
    /// <summary>
    /// Matches templates ON THE WEB SERVER with the Mantra SDK (MatchISO / MatchANSI).
    /// The Mantra files live in FpMfa.MantraFolder (default ~/bin/mantra):
    ///     MANTRA.MFS100.dll, x86\MFS100Dll.dll, x86\iengine_ansi_iso.dll, x64\MFS100Dll.dll, x64\iengine_ansi_iso.dll
    /// The right native folder is chosen for the IIS app pool bitness (32 or 64 bit).
    /// A sub-folder of bin is used so ASP.NET does not try to load these DLLs as web assemblies.
    /// </summary>
    internal static class ServerMatcher
    {
        private static readonly object Sync = new object();
        private static Type _type;
        private static object _device;
        private static bool _initTried;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string path);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool DeleteFile(string path);

        /// <returns>score (>= 0) or -1 with <paramref name="error"/> set</returns>
        public static int Match(byte[] probe, byte[] gallery, bool ansi, out string error)
        {
            error = null;
            lock (Sync)
            {
                try
                {
                    Load();
                    MethodInfo m = Find(ansi ? "MatchANSI" : "MatchISO", 3);
                    if (m == null) { error = "Mantra SDK has no " + (ansi ? "MatchANSI" : "MatchISO"); return -1; }

                    object[] args = { probe, gallery, 0 };
                    int ret = (int)m.Invoke(_device, args);
                    if (ret != 0 && !_initTried)
                    {
                        // some SDK builds want Init() before matching; try once (a scanner on the server is NOT required for this)
                        _initTried = true;
                        try { Find("Init", 0).Invoke(_device, null); } catch { }
                        args = new object[] { probe, gallery, 0 };
                        ret = (int)m.Invoke(_device, args);
                    }
                    if (ret != 0) { error = "Mantra match returned " + ret + ": " + ErrorMsg(ret); return -1; }
                    return Convert.ToInt32(args[2]);
                }
                catch (Exception ex)
                {
                    while (ex.InnerException != null) ex = ex.InnerException;
                    error = ex.GetType().Name + ": " + ex.Message;
                    return -1;
                }
            }
        }

        private static void Load()
        {
            if (_device != null) return;
            string folder = MapFolder(MfaConfig.Current.MantraFolder);
            string sdk = Path.Combine(folder, "MANTRA.MFS100.dll");
            if (!File.Exists(sdk)) throw new FileNotFoundException("Mantra SDK not found: " + sdk);

            string native = Path.Combine(folder, Environment.Is64BitProcess ? "x64" : "x86");
            if (!Directory.Exists(native)) native = folder;
            foreach (string f in Directory.GetFiles(folder, "*.dll", SearchOption.AllDirectories))
                DeleteFile(f + ":Zone.Identifier");   // same as "Unblock" in file properties
            SetDllDirectory(native);                   // so MANTRA.MFS100.dll finds MFS100Dll.dll / iengine_ansi_iso.dll

            Assembly asm = Assembly.LoadFrom(sdk);
            _type = asm.GetType("MANTRA.MFS100");
            if (_type == null) throw new TypeLoadException("MANTRA.MFS100 type not found in " + sdk);
            _device = Activator.CreateInstance(_type);
        }

        private static string MapFolder(string path)
        {
            if (path.StartsWith("~")) return HostingEnvironment.MapPath(path) ?? HttpRuntime.AppDomainAppPath + path.TrimStart('~', '/').Replace('/', '\\');
            return path;
        }

        private static MethodInfo Find(string name, int args)
        {
            return _type.GetMethods().FirstOrDefault(x => x.Name == name && x.GetParameters().Length == args);
        }

        private static string ErrorMsg(int code)
        {
            try { return Convert.ToString(Find("GetErrorMsg", 1).Invoke(_device, new object[] { code })); }
            catch { return "error " + code; }
        }
    }
}
