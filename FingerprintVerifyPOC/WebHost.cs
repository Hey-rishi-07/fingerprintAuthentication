using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace FingerprintVerifyPOC
{
    /// <summary>
    /// Local web server on http://localhost:{HttpPort}/
    ///   GET  /             -> built-in verification page (PCNO box, light, result)
    ///   GET  /api/status   -> scanner + database status
    ///   POST /api/verify   -> {"pcno":"12345"}  => verification result JSON
    /// Any intranet web application (the future transaction page) can call /api/verify from JavaScript.
    /// </summary>
    public class WebHost
    {
        private readonly Settings _s;
        private readonly Verifier _verifier;
        private readonly Scanner _scanner;
        private readonly FingerprintRepository _repo;
        private HttpListener _listener;

        public WebHost(Settings s, Verifier verifier, Scanner scanner, FingerprintRepository repo)
        {
            _s = s;
            _verifier = verifier;
            _scanner = scanner;
            _repo = repo;
        }

        public string BaseUrl { get { return "http://localhost:" + _s.HttpPort + "/"; } }

        public void Run()
        {
            _listener = TryStart(new[] { "http://localhost:" + _s.HttpPort + "/", "http://127.0.0.1:" + _s.HttpPort + "/" })
                     ?? TryStart(new[] { "http://localhost:" + _s.HttpPort + "/" });   // 127.0.0.1 may need admin/urlacl
            if (_listener == null)
                throw new InvalidOperationException("Could not open port " + _s.HttpPort + ". Is the program already running?");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[WEB] Listening on " + BaseUrl + "   (close this window to stop)");
            Console.ResetColor();

            while (true)
            {
                HttpListenerContext ctx;
                try { ctx = _listener.GetContext(); }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }
                ThreadPool.QueueUserWorkItem(o => Handle((HttpListenerContext)o), ctx);
            }
        }

        private static HttpListener TryStart(string[] prefixes)
        {
            var l = new HttpListener();
            foreach (string p in prefixes) l.Prefixes.Add(p);
            try { l.Start(); return l; }
            catch { try { l.Close(); } catch { } return null; }
        }

        private void Handle(HttpListenerContext ctx)
        {
            HttpListenerRequest req = ctx.Request;
            HttpListenerResponse res = ctx.Response;
            try
            {
                AddCors(req, res);
                if (req.HttpMethod == "OPTIONS") { res.StatusCode = 204; res.Close(); return; }

                string path = req.Url.AbsolutePath.TrimEnd('/').ToLowerInvariant();

                if (path == "" || path == "/index.html")
                {
                    Send(res, 200, "text/html; charset=utf-8", WebPage.Html(_s));
                }
                else if (path == "/api/status")
                {
                    string dbMsg;
                    bool db = _repo.TestConnection(out dbMsg);
                    bool dev = _scanner.EnsureInitialized();
                    string json = "{\"scannerReady\":" + (dev ? "true" : "false") +
                                  ",\"scannerMessage\":\"" + Json.Escape(dev ? ("MFS100 ready " + (_scanner.SerialNo ?? "")) : _scanner.LastError) + "\"" +
                                  ",\"databaseReady\":" + (db ? "true" : "false") +
                                  ",\"databaseMessage\":\"" + Json.Escape(dbMsg) + "\"" +
                                  ",\"timeoutSeconds\":" + _s.CaptureTimeoutSeconds +
                                  ",\"blinkMs\":" + (_s.BlinkCount * (_s.BlinkOnMs + _s.BlinkOffMs)) + "}";
                    Send(res, 200, "application/json; charset=utf-8", json);
                }
                else if (path == "/api/verify" && req.HttpMethod == "POST")
                {
                    string body;
                    using (var rd = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8)) body = rd.ReadToEnd();
                    string pcno = Json.Field(body, "pcno") ?? req.QueryString["pcno"];
                    if (pcno == null && body.StartsWith("pcno=", StringComparison.OrdinalIgnoreCase))
                        pcno = Uri.UnescapeDataString(body.Substring(5));

                    Console.WriteLine("[WEB] " + DateTime.Now.ToString("HH:mm:ss") + " verify request for PCNO " + pcno);
                    VerifyResult r = _verifier.Verify(pcno, (stage, msg) => Console.WriteLine("      " + stage + ": " + msg));
                    Program.PrintResult(r);
                    Send(res, 200, "application/json; charset=utf-8", r.ToJson());
                }
                else
                {
                    Send(res, 404, "application/json; charset=utf-8", "{\"error\":\"not found\"}");
                }
            }
            catch (Exception ex)
            {
                try { Send(res, 500, "application/json; charset=utf-8", "{\"error\":\"" + Json.Escape(ex.Message) + "\"}"); } catch { }
            }
        }

        private void AddCors(HttpListenerRequest req, HttpListenerResponse res)
        {
            string origin = req.Headers["Origin"];
            string allowed = _s.AllowedOrigins ?? "";
            if (allowed == "*")
            {
                res.Headers["Access-Control-Allow-Origin"] = "*";
            }
            else if (!string.IsNullOrEmpty(origin) &&
                     allowed.Split(',').Select(a => a.Trim()).Any(a => a.Equals(origin, StringComparison.OrdinalIgnoreCase)))
            {
                res.Headers["Access-Control-Allow-Origin"] = origin;
                res.Headers["Vary"] = "Origin";
            }
            res.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
            res.Headers["Access-Control-Allow-Headers"] = "Content-Type";
            // Chrome/Edge "Private Network Access": lets an intranet site call this localhost service
            res.Headers["Access-Control-Allow-Private-Network"] = "true";
        }

        private static void Send(HttpListenerResponse res, int code, string type, string body)
        {
            byte[] b = Encoding.UTF8.GetBytes(body);
            res.StatusCode = code;
            res.ContentType = type;
            res.Headers["Cache-Control"] = "no-store";
            res.ContentLength64 = b.Length;
            res.OutputStream.Write(b, 0, b.Length);
            res.OutputStream.Close();
        }
    }
}
