using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using FingerprintVerifyPOC;   // Scanner.cs is shared with the POC project

namespace FpScannerAgent
{
    /// <summary>
    /// Fingerprint Scanner Agent - runs hidden in the background on each user PC.
    ///
    /// It ONLY drives the Mantra MFS100 scanner. It has no database connection, no table names and
    /// does no matching: the web server does the verification.
    ///
    ///   GET  http://localhost:8036/status   -> {"agent":"FpScannerAgent","scannerReady":true,...}
    ///   POST http://localhost:8036/capture  -> {"timeout":10,"blink":true}
    ///                                          => {"ok":true,"quality":78,"iso":"<base64>","ansi":"<base64>"}
    /// </summary>
    internal static class AgentProgram
    {
        public const string Version = "1.0.0";

        [STAThread]
        private static int Main(string[] args)
        {
            // one agent per Windows session (it is started at every logon and by fpscanneragent:// links)
            bool created;
            var single = new Mutex(true, @"Local\FpScannerAgent", out created);
            if (!created) return 0;

            try
            {
                var cfg = AgentConfig.Load();
                Log.Info("Agent " + Version + " starting, port " + cfg.Port + ", allowed origins: " + cfg.AllowedOrigins);
                using (var scanner = new Scanner())
                {
                    bool ready = scanner.EnsureInitialized();
                    Log.Info(ready ? "Scanner ready, serial " + scanner.SerialNo : "Scanner not ready: " + scanner.LastError);
                    new AgentServer(cfg, scanner).RunForever();
                }
            }
            catch (Exception ex)
            {
                Log.Info("FATAL: " + ex);
                return 1;
            }
            finally
            {
                GC.KeepAlive(single);
            }
            return 0;
        }
    }

    internal class AgentConfig
    {
        public int Port;
        public string AllowedOrigins;
        public int MinQuality;
        public int MaxTimeoutSeconds;
        public int BlinkCount, BlinkOnMs, BlinkOffMs, BlinkSettleMs;

        public static AgentConfig Load()
        {
            return new AgentConfig
            {
                Port = Int("Port", 8036),
                AllowedOrigins = Str("AllowedOrigins", "*"),
                MinQuality = Int("MinQuality", 40),
                MaxTimeoutSeconds = Int("MaxTimeoutSeconds", 30),
                BlinkCount = Int("BlinkCount", 3),
                BlinkOnMs = Int("BlinkOnMs", 250),
                BlinkOffMs = Int("BlinkOffMs", 250),
                BlinkSettleMs = Int("BlinkSettleMs", 700)
            };
        }

        private static string Str(string k, string d) { string v = ConfigurationManager.AppSettings[k]; return v == null ? d : v.Trim(); }
        private static int Int(string k, int d) { int v; return int.TryParse(ConfigurationManager.AppSettings[k], out v) ? v : d; }
    }

    internal class AgentServer
    {
        private readonly AgentConfig _cfg;
        private readonly Scanner _scanner;
        private readonly object _busy = new object();

        public AgentServer(AgentConfig cfg, Scanner scanner)
        {
            _cfg = cfg;
            _scanner = scanner;
        }

        /// <summary>Listens forever; if the port cannot be opened, retries every 30 s.</summary>
        public void RunForever()
        {
            while (true)
            {
                HttpListener l = Start();
                if (l == null)
                {
                    Log.Info("Could not open port " + _cfg.Port + ", retrying in 30 s");
                    Thread.Sleep(30000);
                    continue;
                }
                Log.Info("Listening on http://localhost:" + _cfg.Port + "/");
                try
                {
                    while (l.IsListening)
                    {
                        HttpListenerContext ctx = l.GetContext();
                        ThreadPool.QueueUserWorkItem(o => Handle((HttpListenerContext)o), ctx);
                    }
                }
                catch (Exception ex)
                {
                    Log.Info("Listener stopped: " + ex.Message);
                }
                try { l.Close(); } catch { }
                Thread.Sleep(2000);
            }
        }

        private HttpListener Start()
        {
            foreach (string[] prefixes in new[]
            {
                new[] { "http://localhost:" + _cfg.Port + "/", "http://127.0.0.1:" + _cfg.Port + "/" },
                new[] { "http://localhost:" + _cfg.Port + "/" }
            })
            {
                var l = new HttpListener();
                foreach (string p in prefixes) l.Prefixes.Add(p);
                try { l.Start(); return l; }
                catch { try { l.Close(); } catch { } }
            }
            return null;
        }

        private void Handle(HttpListenerContext ctx)
        {
            HttpListenerRequest req = ctx.Request;
            HttpListenerResponse res = ctx.Response;
            try
            {
                string origin = req.Headers["Origin"];
                if (!string.IsNullOrEmpty(origin) && !OriginAllowed(origin))
                {
                    Log.Info("Rejected request from origin " + origin);
                    Send(res, 403, "{\"ok\":false,\"message\":\"Origin not allowed\"}");
                    return;
                }
                res.Headers["Access-Control-Allow-Origin"] = string.IsNullOrEmpty(origin) ? "*" : origin;
                res.Headers["Vary"] = "Origin";
                res.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
                res.Headers["Access-Control-Allow-Headers"] = "Content-Type";
                res.Headers["Access-Control-Allow-Private-Network"] = "true";
                if (req.HttpMethod == "OPTIONS") { res.StatusCode = 204; res.Close(); return; }

                string path = req.Url.AbsolutePath.TrimEnd('/').ToLowerInvariant();
                if (path == "" || path == "/status") Status(res);
                else if (path == "/capture" && req.HttpMethod == "POST") Capture(req, res);
                else Send(res, 404, "{\"ok\":false,\"message\":\"not found\"}");
            }
            catch (Exception ex)
            {
                Log.Info("Request error: " + ex.Message);
                try { Send(res, 500, "{\"ok\":false,\"message\":\"" + Esc(ex.Message) + "\"}"); } catch { }
            }
        }

        private bool OriginAllowed(string origin)
        {
            if (_cfg.AllowedOrigins == "*") return true;
            return _cfg.AllowedOrigins.Split(',').Select(a => a.Trim().TrimEnd('/'))
                .Any(a => a.Length > 0 && a.Equals(origin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
        }

        private void Status(HttpListenerResponse res)
        {
            bool ready = _scanner.EnsureInitialized();
            Send(res, 200, "{\"agent\":\"FpScannerAgent\",\"version\":\"" + AgentProgram.Version + "\"" +
                           ",\"scannerReady\":" + (ready ? "true" : "false") +
                           ",\"serial\":\"" + Esc(_scanner.SerialNo) + "\",\"model\":\"" + Esc(_scanner.Model) + "\"" +
                           ",\"message\":\"" + Esc(ready ? "Scanner ready" : _scanner.LastError) + "\"}");
        }

        private void Capture(HttpListenerRequest req, HttpListenerResponse res)
        {
            string body;
            using (var rd = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8)) body = rd.ReadToEnd();
            int timeout = Num(body, "timeout", 10);
            if (timeout < 5) timeout = 5;
            if (timeout > _cfg.MaxTimeoutSeconds) timeout = _cfg.MaxTimeoutSeconds;
            bool blink = !Regex.IsMatch(body, "\"blink\"\\s*:\\s*false", RegexOptions.IgnoreCase);

            if (!Monitor.TryEnter(_busy))
            {
                Send(res, 200, "{\"ok\":false,\"code\":\"BUSY\",\"message\":\"Scanner is busy. Try again.\"}");
                return;
            }
            try
            {
                if (!_scanner.EnsureInitialized())
                {
                    Send(res, 200, "{\"ok\":false,\"code\":\"NO_SCANNER\",\"message\":\"" + Esc("Fingerprint scanner not connected: " + _scanner.LastError) + "\"}");
                    return;
                }
                if (blink) _scanner.Blink(_cfg.BlinkCount, _cfg.BlinkOnMs, _cfg.BlinkOffMs, _cfg.BlinkSettleMs);

                byte[] iso, ansi;
                int quality;
                int ret = _scanner.Capture(timeout * 1000, _cfg.MinQuality, null, out iso, out ansi, out quality);
                if (ret != 0 || iso == null)
                {
                    bool to = ret == Scanner.ErrTimeout || ret == Scanner.ErrFingerNotProper;
                    Log.Info("Capture failed " + ret + ": " + _scanner.LastError);
                    Send(res, 200, "{\"ok\":false,\"code\":\"" + (to ? "TIMEOUT" : "CAPTURE_ERROR") + "\",\"message\":\"" +
                                   Esc(to ? "No finger detected within " + timeout + " seconds." : "Capture failed: " + _scanner.LastError) + "\"}");
                    return;
                }
                Log.Info("Captured, quality " + quality);
                Send(res, 200, "{\"ok\":true,\"quality\":" + quality +
                               ",\"iso\":\"" + Convert.ToBase64String(iso) + "\"" +
                               ",\"ansi\":\"" + (ansi != null ? Convert.ToBase64String(ansi) : "") + "\"}");
            }
            finally
            {
                Monitor.Exit(_busy);
            }
        }

        private static int Num(string json, string name, int def)
        {
            Match m = Regex.Match(json ?? "", "\"" + name + "\"\\s*:\\s*\"?(\\d+)", RegexOptions.IgnoreCase);
            int v;
            return m.Success && int.TryParse(m.Groups[1].Value, out v) ? v : def;
        }

        private static void Send(HttpListenerResponse res, int code, string json)
        {
            byte[] b = Encoding.UTF8.GetBytes(json);
            res.StatusCode = code;
            res.ContentType = "application/json; charset=utf-8";
            res.Headers["Cache-Control"] = "no-store";
            res.ContentLength64 = b.Length;
            res.OutputStream.Write(b, 0, b.Length);
            res.OutputStream.Close();
        }

        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < 0x20) sb.Append(' ');
                else if (c == '<') sb.Append("\\u003c");
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }

    /// <summary>Small log in %LOCALAPPDATA%\FpScannerAgent\agent.log (restarted when it passes 1 MB).</summary>
    internal static class Log
    {
        private static readonly object Sync = new object();
        private static readonly string File_ = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FpScannerAgent", "agent.log");

        public static void Info(string msg)
        {
            lock (Sync)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(File_));
                    var fi = new FileInfo(File_);
                    if (fi.Exists && fi.Length > 1024 * 1024) fi.Delete();
                    File.AppendAllText(File_, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine);
                }
                catch { }
            }
        }
    }
}
