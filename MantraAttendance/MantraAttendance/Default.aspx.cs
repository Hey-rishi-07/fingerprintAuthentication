using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace MantraAttendance
{
    public partial class _Default : Page
    {
        private static readonly string TemplatesDir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "App_Data", "Templates");
        private static readonly string IndexFile = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "App_Data", "templates.json");
        private static readonly object _lock = new object();
        private const int MatchThreshold = 1400;
        private const int CaptureTimeout = 10000;

        private bool IsDemoMode
        {
            get { return chkDemoMode.Checked; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                EnsureDirectories();
                LoadEnrolledUsers();
                TryInitDevice();
                ScriptManager.RegisterStartupScript(this, GetType(), "initUI",
                    "toggleFields();updateDemoUI();", true);
            }
        }

        private void TryInitDevice()
        {
            var dev = DeviceManager.Instance;
            if (dev.IsInitialized)
            {
                lblDeviceStatus.CssClass = "status";
                lblDeviceStatus.Text = "Scanner: Connected & Ready";
                return;
            }

            int ret = dev.InitDevice();
            if (ret == 0)
            {
                lblDeviceStatus.CssClass = "status";
                lblDeviceStatus.Text = "Scanner: Connected & Ready";
            }
            else if (ret == -999)
            {
                lblDeviceStatus.CssClass = "status";
                lblDeviceStatus.Text = "MFS100 SDK dll not found in bin or installed paths. Click Re-init after placing MANTRA.MFS100.dll in bin.";
                if (!string.IsNullOrEmpty(dev.LastError))
                    lblDeviceStatus.Text += " [" + dev.LastError + "]";
            }
            else
            {
                lblDeviceStatus.CssClass = "status error";
                lblDeviceStatus.Text = "Scanner init failed (code " + ret + "). " +
                    (string.IsNullOrEmpty(dev.LastError) ? "Check USB connection." : dev.LastError);
            }
        }

        protected void chkDemoMode_CheckedChanged(object sender, EventArgs e)
        {
            if (IsDemoMode)
            {
                lblDeviceStatus.CssClass = "status";
                lblDeviceStatus.Text = "Running in DEMO MODE (no scanner required).";
                lblDemoBadge.Visible = true;
            }
            else
            {
                lblDemoBadge.Visible = false;
                TryInitDevice();
            }
            LoadEnrolledUsers();
            ScriptManager.RegisterStartupScript(this, GetType(), "toggleUI",
                "toggleFields();updateDemoUI();", true);
        }

        protected void btnInit_Click(object sender, EventArgs e)
        {
            DeviceManager.Instance.UninitDevice();
            TryInitDevice();
        }

        protected void btnRefresh_Click(object sender, EventArgs e)
        {
            LoadEnrolledUsers();
            ShowStatus("User list refreshed.", false, true);
        }

        protected void btnCapture_Click(object sender, EventArgs e)
        {
            pnlResult.Visible = false;

            if (rbEnroll.Checked)
            {
                string name = txtName.Text.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    ShowStatus("Please enter the person's name.", true);
                    return;
                }
                Enroll(name);
            }
            else
            {
                Verify();
            }
        }

        private void Enroll(string name)
        {
            byte[] template = CaptureTemplate();
            if (template == null) return;

            lock (_lock)
            {
                EnsureDirectories();
                string existingFile = FindExistingTemplate(name);
                if (existingFile != null)
                {
                    File.WriteAllBytes(existingFile, template);
                }
                else
                {
                    string fileName = Guid.NewGuid().ToString("N") + ".dat";
                    string filePath = Path.Combine(TemplatesDir, fileName);
                    File.WriteAllBytes(filePath, template);
                    AddToIndex(name, fileName);
                }
            }

            ShowStatus("Enrollment successful for " + name + "!", false, true);
            LoadEnrolledUsers();
        }

        private void Verify()
        {
            if (IsDemoMode)
            {
                string selectedUser = ddlDemoUser.SelectedValue;
                if (string.IsNullOrEmpty(selectedUser))
                {
                    ShowStatus("No users enrolled. Enroll someone first, or select a user from the dropdown.", true);
                    return;
                }
                pnlResult.Visible = true;
                pnlResult.CssClass = "result match";
                lblResult.Text = "Welcome, " + selectedUser + "!";
                return;
            }

            if (!DeviceManager.Instance.IsInitialized)
            {
                int ret = DeviceManager.Instance.InitDevice();
                if (ret != 0)
                {
                    ShowStatus("Scanner not available. Enable Demo Mode to test without device.", true);
                    return;
                }
            }

            byte[] template = CaptureTemplate();
            if (template == null) return;

            var index = LoadIndex();
            if (index == null || index.Count == 0)
            {
                ShowStatus("No enrolled users found. Enroll someone first.", true);
                return;
            }

            string matchedName = null;
            int bestScore = 0;

            var dev = DeviceManager.Instance;
            lock (_lock)
            {
                foreach (var entry in index)
                {
                    string filePath = Path.Combine(TemplatesDir, entry.File);
                    if (!File.Exists(filePath)) continue;

                    byte[] storedTemplate = File.ReadAllBytes(filePath);
                    int score = 0;
                    int ret = dev.InvokeMethod<int>("MatchISO",
                        new object[] { storedTemplate, template, 0 },
                        new int[] { 2 });

                    if (ret == 0)
                    {
                        score = (int)dev.GetRefArg("MatchISO", 2);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            matchedName = entry.Name;
                        }
                    }
                }
            }

            pnlResult.Visible = true;
            if (matchedName != null && bestScore >= MatchThreshold)
            {
                pnlResult.CssClass = "result match";
                lblResult.Text = "Welcome, " + matchedName + "!  (Score: " + bestScore + ")";
            }
            else
            {
                pnlResult.CssClass = "result nomatch";
                lblResult.Text = "Fingerprint not recognized.";
            }
        }

        private byte[] CaptureTemplate()
        {
            if (IsDemoMode)
            {
                byte[] dummy = new byte[400];
                new Random().NextBytes(dummy);
                return dummy;
            }

            try
            {
                var dev = DeviceManager.Instance;
                dynamic device = dev.Device;

                bool connected = device.IsConnected();
                if (!connected)
                {
                    ShowStatus("MFS100 device not connected. Plug it in and try again.", true);
                    return null;
                }

                object fingerData = null;
                int ret = dev.InvokeMethod<int>("AutoCapture",
                    new object[] { null, CaptureTimeout, false, true },
                    new int[] { 0 });

                if (ret != 0)
                {
                    string errMsg = device.GetErrorMsg(ret);
                    ShowStatus("Capture failed: " + errMsg, true);
                    return null;
                }

                fingerData = dev.GetRefArg("AutoCapture", 0);
                if (fingerData == null)
                {
                    ShowStatus("Capture returned no data.", true);
                    return null;
                }

                dynamic fd = fingerData;
                int quality = fd.Quality;

                if (quality < 40)
                {
                    ShowStatus("Poor fingerprint quality (" + quality + "/100). Try again.", true);
                    return null;
                }

                return (byte[])fd.ISOTemplate;
            }
            catch (Exception ex)
            {
                ShowStatus("Capture error: " + ex.Message, true);
                return null;
            }
        }

        private void ShowStatus(string message, bool isError = false, bool isSuccess = false)
        {
            lblStatus.Visible = true;
            lblStatus.Text = message;
            if (isError)
                lblStatus.CssClass = "status error";
            else if (isSuccess)
                lblStatus.CssClass = "status success";
            else
                lblStatus.CssClass = "status";
        }

        private void EnsureDirectories()
        {
            if (!Directory.Exists(TemplatesDir))
                Directory.CreateDirectory(TemplatesDir);
        }

        private string FindExistingTemplate(string name)
        {
            var index = LoadIndex();
            TemplateEntry entry = null;
            if (index != null)
                entry = index.FirstOrDefault(e =>
                    e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (entry != null)
            {
                string path = Path.Combine(TemplatesDir, entry.File);
                if (File.Exists(path)) return path;
            }
            return null;
        }

        private void AddToIndex(string name, string fileName)
        {
            var index = LoadIndex() ?? new List<TemplateEntry>();
            index.Add(new TemplateEntry { Name = name, File = fileName });
            SaveIndex(index);
        }

        private List<TemplateEntry> LoadIndex()
        {
            if (!File.Exists(IndexFile)) return new List<TemplateEntry>();
            try
            {
                string json = File.ReadAllText(IndexFile);
                var serializer = new JavaScriptSerializer();
                return serializer.Deserialize<List<TemplateEntry>>(json);
            }
            catch
            {
                return new List<TemplateEntry>();
            }
        }

        private void SaveIndex(List<TemplateEntry> index)
        {
            var serializer = new JavaScriptSerializer();
            string json = serializer.Serialize(index);
            File.WriteAllText(IndexFile, json);
        }

        private void LoadEnrolledUsers()
        {
            var index = LoadIndex();
            if (index == null || index.Count == 0)
            {
                blEnrolled.Items.Clear();
                lblEmpty.Visible = true;
                lblCount.Text = "0";
                ddlDemoUser.Items.Clear();
                ddlDemoUser.Items.Add(new ListItem("-- Select User --", ""));
                return;
            }

            lblEmpty.Visible = false;
            lblCount.Text = index.Count.ToString();
            blEnrolled.Items.Clear();
            ddlDemoUser.Items.Clear();
            ddlDemoUser.Items.Add(new ListItem("-- Select User --", ""));

            foreach (var entry in index.OrderBy(e => e.Name))
            {
                blEnrolled.Items.Add(entry.Name);
                ddlDemoUser.Items.Add(new ListItem(entry.Name, entry.Name));
            }
        }
    }

    public class TemplateEntry
    {
        public string Name { get; set; }
        public string File { get; set; }
    }

    public sealed class DeviceManager
    {
        private static readonly Lazy<DeviceManager> _instance =
            new Lazy<DeviceManager>(() => new DeviceManager());
        public static DeviceManager Instance { get { return _instance.Value; } }

        public dynamic Device { get; private set; }
        public bool IsInitialized { get; private set; }
        public string LastError { get; private set; }

        private Type _deviceType;
        private System.Reflection.Assembly _sdkAssembly;
        private readonly Dictionary<string, System.Reflection.MethodInfo> _methodCache =
            new Dictionary<string, System.Reflection.MethodInfo>();
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
                        LastError = "Could not find MANTRA.MFS100 type. Ensure the SDK DLL is in the bin folder.";
                        return -999;
                    }

                    Device = Activator.CreateInstance(_deviceType);
                }

                int ret = Device.Init();
                IsInitialized = ret == 0;
                if (!IsInitialized)
                    LastError = "Init() returned code " + ret + ". Device may not be connected or driver not installed.";
                return ret;
            }
            catch (Exception ex)
            {
                IsInitialized = false;
                LastError = ex.GetType().Name + ": " + ex.Message;
                if (ex.InnerException != null)
                    LastError += " | Inner: " + ex.InnerException.Message;
                return -1;
            }
        }

        private Type ResolveMfs100Type()
        {
            Type t = Type.GetType("MANTRA.MFS100, MANTRA.MFS100", false)
                ?? Type.GetType("MANTRA.MFS100, MANTRA.MFS100.Net", false)
                ?? Type.GetType("MANTRA.MFS100, MANTRA.MFS100, Version=9.0.2.5, Culture=neutral, PublicKeyToken=fedab34666068406", false)
                ?? Type.GetType("MFS100, MFS100.Net", false)
                ?? Type.GetType("Mantra.MFS100.MFS100, Mantra.MFS100", false)
                ?? Type.GetTypeFromProgID("MFS100.MFS100", false);
            if (t != null) return t;

            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name.IndexOf("MFS100",
                    StringComparison.OrdinalIgnoreCase) >= 0);
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
                Path.Combine(binDir, "MANTRA.MFS100.Net.dll"),
                Path.Combine(appRoot, "Lib", "MANTRA.MFS100.dll"),
                @"C:\Program Files\Mantra\MFS100 SDK\SDK\Dotnet\MANTRA.MFS100.dll",
                @"C:\Program Files (x86)\Mantra\MFS100 SDK\SDK\Dotnet\MANTRA.MFS100.dll",
                @"C:\Program Files\Mantra\MFS100 SDK\SDK\Dot Net\MANTRA.MFS100.dll",
                @"C:\Program Files (x86)\Mantra\MFS100 SDK\SDK\Dot Net\MANTRA.MFS100.dll",
            };

            foreach (string path in searchPaths)
            {
                if (!File.Exists(path)) continue;
                try
                {
                    _sdkAssembly = System.Reflection.Assembly.LoadFrom(path);
                    t = _sdkAssembly.GetType("MANTRA.MFS100");
                    if (t == null)
                        t = _sdkAssembly.GetTypes().FirstOrDefault(x => x.Name == "MFS100");
                    if (t != null) return t;
                }
                catch { }
            }

            return null;
        }

        public T InvokeMethod<T>(string methodName, object[] parameters, int[] refParamIndices)
        {
            if (_deviceType == null) return default(T);

            System.Reflection.MethodInfo method;
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
    }
}
