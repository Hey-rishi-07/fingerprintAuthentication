using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace FingerprintVerifyPOC
{
    public enum VerifyStatus
    {
        VERIFIED = 0,
        NOT_MATCHED = 1,
        TIMEOUT = 2,
        NOT_ENROLLED = 3,
        DEVICE_ERROR = 4,
        DB_ERROR = 5,
        INVALID_PCNO = 6,
        BUSY = 7
    }

    public class VerifyResult
    {
        public VerifyStatus Status;
        public string Pcno;
        public string Message;
        public int BestScore;
        public int Threshold;
        public int Quality;
        public int TemplatesChecked;
        public string MatchedFinger;
        public long DurationMs;
        public DateTime Time = DateTime.Now;

        public bool Verified { get { return Status == VerifyStatus.VERIFIED; } }

        public string ToJson()
        {
            return "{" +
                "\"verified\":" + (Verified ? "true" : "false") + "," +
                "\"status\":\"" + Status + "\"," +
                "\"pcno\":\"" + Json.Escape(Pcno) + "\"," +
                "\"message\":\"" + Json.Escape(Message) + "\"," +
                "\"score\":" + BestScore + "," +
                "\"threshold\":" + Threshold + "," +
                "\"quality\":" + Quality + "," +
                "\"templatesChecked\":" + TemplatesChecked + "," +
                "\"matchedFinger\":\"" + Json.Escape(MatchedFinger) + "\"," +
                "\"durationMs\":" + DurationMs + "," +
                "\"time\":\"" + Time.ToString("yyyy-MM-ddTHH:mm:ss") + "\"" +
                "}";
        }
    }

    /// <summary>
    /// The whole POC flow:  PCNO -> load all templates from Oracle -> blink -> wait for finger (10 s)
    /// -> match against every enrolled finger -> result.
    /// One verification at a time (there is only one scanner).
    /// </summary>
    public class Verifier
    {
        private readonly Settings _s;
        private readonly Scanner _scanner;
        private readonly FingerprintRepository _repo;
        private readonly object _busy = new object();

        public Verifier(Settings s, Scanner scanner, FingerprintRepository repo)
        {
            _s = s;
            _scanner = scanner;
            _repo = repo;
        }

        /// <param name="progress">receives stage keys: LOADING, BLINK, WAITING, MATCHING plus messages</param>
        public VerifyResult Verify(string pcno, Action<string, string> progress)
        {
            if (!System.Threading.Monitor.TryEnter(_busy))
            {
                return new VerifyResult { Status = VerifyStatus.BUSY, Pcno = pcno, Threshold = _s.MatchThreshold,
                                          Message = "Scanner is busy with another verification. Try again." };
            }
            try
            {
                var sw = Stopwatch.StartNew();
                VerifyResult r = DoVerify((pcno ?? "").Trim(), progress ?? delegate { });
                r.DurationMs = sw.ElapsedMilliseconds;
                Log(r);
                return r;
            }
            finally
            {
                System.Threading.Monitor.Exit(_busy);
            }
        }

        private VerifyResult DoVerify(string pcno, Action<string, string> progress)
        {
            var r = new VerifyResult { Pcno = pcno, Threshold = _s.MatchThreshold };

            if (!Regex.IsMatch(pcno, _s.PcnoPattern))
            {
                r.Status = VerifyStatus.INVALID_PCNO;
                r.Message = "Please enter a valid PC No.";
                return r;
            }

            // 1. Enrolled templates (can be several fingers per PCNO)
            progress("LOADING", "Fetching enrolled fingerprints for " + pcno + " ...");
            List<StoredTemplate> templates;
            try
            {
                templates = _repo.GetTemplates(pcno);
            }
            catch (Exception ex)
            {
                r.Status = VerifyStatus.DB_ERROR;
                r.Message = "Database error: " + ex.Message;
                return r;
            }
            if (templates.Count == 0)
            {
                r.Status = VerifyStatus.NOT_ENROLLED;
                r.Message = "No fingerprint is enrolled for PC No " + pcno + ".";
                return r;
            }

            // 2. Scanner
            if (!_scanner.EnsureInitialized())
            {
                r.Status = VerifyStatus.DEVICE_ERROR;
                r.Message = "Fingerprint scanner not ready: " + _scanner.LastError;
                return r;
            }

            // 3. Blink -> light stays on and waits up to N seconds for a finger
            progress("BLINK", "Get ready...");
            _scanner.Blink(_s.BlinkCount, _s.BlinkOnMs, _s.BlinkOffMs, _s.BlinkSettleMs);

            progress("WAITING", "Place your finger on the scanner (" + _s.CaptureTimeoutSeconds + " s)");
            byte[] iso, ansi;
            int quality;
            int ret = _scanner.Capture(_s.CaptureTimeoutSeconds * 1000, _s.MinQuality,
                                       msg => progress("NOTICE", msg), out iso, out ansi, out quality);
            r.Quality = quality;
            if (ret != 0)
            {
                bool timeout = ret == Scanner.ErrTimeout || ret == Scanner.ErrFingerNotProper;
                r.Status = timeout ? VerifyStatus.TIMEOUT : VerifyStatus.DEVICE_ERROR;
                r.Message = timeout ? "No finger detected within " + _s.CaptureTimeoutSeconds + " seconds."
                                    : "Capture failed: " + _scanner.LastError;
                return r;
            }

            if ((iso == null || iso.Length == 0) && (ansi == null || ansi.Length == 0))
            {
                r.Status = VerifyStatus.DEVICE_ERROR;
                r.Message = "Scanner returned an empty template.";
                return r;
            }

            // 4. Match the live finger against EVERY enrolled finger, keep the best score
            progress("MATCHING", "Matching against " + templates.Count + " enrolled fingerprint(s)...");
            int best = 0;
            string lastErr = null;
            foreach (StoredTemplate t in templates)
            {
                string err, method;
                int score = BestMatch(iso, ansi, t.Data, out method, out err);
                r.TemplatesChecked++;
                if (score < 0) { lastErr = err; continue; }
                if (score > best) { best = score; r.MatchedFinger = t.FingerLabel; }
                if (best >= _s.MatchThreshold) break;   // good enough, stop early
            }
            r.BestScore = best;

            if (best >= _s.MatchThreshold)
            {
                r.Status = VerifyStatus.VERIFIED;
                r.Message = "Fingerprint verified.";
            }
            else
            {
                r.Status = VerifyStatus.NOT_MATCHED;
                r.MatchedFinger = null;
                r.Message = "Fingerprint did not match." +
                            (best == 0 && lastErr != null ? " (" + lastErr + ")" : "");
            }
            return r;
        }

        /// <summary>Matches with MatchISO and/or MatchANSI (TemplateFormat) and returns the best score, -1 if all failed.</summary>
        public int BestMatch(byte[] liveIso, byte[] liveAnsi, byte[] stored, out string method, out string error)
        {
            method = null;
            error = null;
            int best = -1;
            string err;
            if (_s.TryIso && liveIso != null && liveIso.Length > 0)
            {
                int sc = _scanner.Match(liveIso, stored, false, out err);
                if (sc < 0) error = "ISO: " + err;
                else if (sc > best) { best = sc; method = "ISO"; }
            }
            if (_s.TryAnsi && liveAnsi != null && liveAnsi.Length > 0)
            {
                int sc = _scanner.Match(liveAnsi, stored, true, out err);
                if (sc < 0) error = (error == null ? "" : error + "; ") + "ANSI: " + err;
                else if (sc > best) { best = sc; method = "ANSI"; }
            }
            return best;
        }

        /// <summary>Daily local log - useful to calibrate MatchThreshold from real scores during the POC.</summary>
        private void Log(VerifyResult r)
        {
            try
            {
                string dir = Path.IsPathRooted(_s.LogFolder)
                    ? _s.LogFolder
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _s.LogFolder);
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "verify_" + DateTime.Now.ToString("yyyyMMdd") + ".csv");
                bool header = !File.Exists(file);
                var sb = new StringBuilder();
                if (header) sb.AppendLine("Time,Machine,WindowsUser,PCNO,Status,Score,Threshold,Quality,Templates,Finger,DurationMs");
                sb.AppendLine(string.Join(",", new[] {
                    r.Time.ToString("yyyy-MM-dd HH:mm:ss"), Environment.MachineName, Environment.UserName,
                    r.Pcno, r.Status.ToString(), r.BestScore.ToString(), r.Threshold.ToString(),
                    r.Quality.ToString(), r.TemplatesChecked.ToString(), r.MatchedFinger ?? "", r.DurationMs.ToString() }));
                File.AppendAllText(file, sb.ToString());
            }
            catch { }
        }
    }

    internal static class Json
    {
        public static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': break;
                    case '\t': sb.Append("\\t"); break;
                    case '<': sb.Append("\\u003c"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>Tiny extractor for {"pcno":"123"} or {"pcno":123} - avoids a JSON library dependency.</summary>
        public static string Field(string json, string name)
        {
            if (string.IsNullOrEmpty(json)) return null;
            Match m = Regex.Match(json, "\"" + Regex.Escape(name) + "\"\\s*:\\s*(\"(?<s>[^\"]*)\"|(?<n>[0-9A-Za-z_-]+))",
                                  RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            return m.Groups["s"].Success ? m.Groups["s"].Value : m.Groups["n"].Value;
        }
    }
}
