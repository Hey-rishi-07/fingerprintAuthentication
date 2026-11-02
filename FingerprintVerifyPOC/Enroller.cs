using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace FingerprintVerifyPOC
{
    /// <summary>
    /// Enrolment into the POC's own table:
    ///   capture 1 -> lift -> capture 2 -> the two captures must match each other (same finger, good placement)
    ///   -> the better-quality ISO template is saved for (PCNO, finger).
    /// Captures are kept in memory only (5 minutes) and referenced by a random token - templates never go to the browser.
    /// </summary>
    public class Enroller
    {
        private class Cap { public byte[] Iso; public int Quality; public DateTime Time; }

        private readonly Settings _s;
        private readonly Scanner _scanner;
        private readonly EnrollRepository _repo;
        private readonly Dictionary<string, Cap> _caps = new Dictionary<string, Cap>();

        public Enroller(Settings s, Scanner scanner, EnrollRepository repo)
        {
            _s = s;
            _scanner = scanner;
            _repo = repo;
        }

        /// <summary>One capture for enrolment. Returns JSON {ok, token, quality, message}.</summary>
        public string CaptureJson(bool blink)
        {
            string token, message;
            int quality;
            bool ok = Capture(blink, out token, out quality, out message);
            return "{\"ok\":" + (ok ? "true" : "false") + ",\"token\":\"" + Json.Escape(token) + "\",\"quality\":" + quality +
                   ",\"message\":\"" + Json.Escape(message) + "\"}";
        }

        public bool Capture(bool blink, out string token, out int quality, out string message)
        {
            token = null;
            quality = 0;
            if (!_scanner.EnsureInitialized()) { message = "Scanner not ready: " + _scanner.LastError; return false; }
            if (blink) _scanner.Blink(_s.BlinkCount, _s.BlinkOnMs, _s.BlinkOffMs, _s.BlinkSettleMs);

            byte[] iso, ansi;
            int ret = _scanner.Capture(_s.CaptureTimeoutSeconds * 1000, _s.EnrollMinQuality, null, out iso, out ansi, out quality);
            if (ret != 0 || iso == null || iso.Length == 0)
            {
                message = ret == Scanner.ErrTimeout || ret == Scanner.ErrFingerNotProper
                    ? "No finger detected within " + _s.CaptureTimeoutSeconds + " seconds."
                    : "Capture failed: " + _scanner.LastError;
                return false;
            }
            if (quality < _s.EnrollMinQuality)
            {
                message = "Quality " + quality + " is too low for enrolment (minimum " + _s.EnrollMinQuality + "). Clean the glass / finger and try again.";
                return false;
            }

            token = Guid.NewGuid().ToString("N");
            lock (_caps)
            {
                // forget old captures
                var old = new List<string>();
                foreach (var kv in _caps) if ((DateTime.Now - kv.Value.Time).TotalMinutes > 5) old.Add(kv.Key);
                foreach (string k in old) _caps.Remove(k);
                _caps[token] = new Cap { Iso = iso, Quality = quality, Time = DateTime.Now };
            }
            message = "Captured, quality " + quality;
            return true;
        }

        /// <summary>Compares the two captures and saves. Returns JSON {ok, message, score, quality}.</summary>
        public string EnrollJson(string pcno, string finger, string token1, string token2, string by)
        {
            int score, quality;
            string message;
            bool ok = Enroll(pcno, finger, token1, token2, by, out score, out quality, out message);
            return "{\"ok\":" + (ok ? "true" : "false") + ",\"message\":\"" + Json.Escape(message) + "\",\"score\":" + score +
                   ",\"quality\":" + quality + "}";
        }

        public bool Enroll(string pcno, string finger, string token1, string token2, string by,
                           out int score, out int quality, out string message)
        {
            score = 0;
            quality = 0;
            pcno = (pcno ?? "").Trim();
            int fno;
            if (!Regex.IsMatch(pcno, _s.PcnoPattern)) { message = "Please enter a valid PC No."; return false; }
            if (!int.TryParse(finger, out fno) || fno < 1 || fno > 10) { message = "Choose a finger (1-10)."; return false; }

            Cap c1, c2;
            lock (_caps)
            {
                _caps.TryGetValue(token1 ?? "", out c1);
                _caps.TryGetValue(token2 ?? "", out c2);
                if (token1 != null) _caps.Remove(token1);
                if (token2 != null) _caps.Remove(token2);
            }
            if (c1 == null || c2 == null) { message = "Captures expired - please start again."; return false; }

            string err;
            score = _scanner.Match(c2.Iso, c1.Iso, false, out err);
            if (score < 0) { message = "Could not compare the two captures: " + err; return false; }
            if (score < _s.MatchThreshold)
            {
                message = "The two scans do not match each other (score " + score + ", needs " + _s.MatchThreshold +
                          "). Use the same finger, place it flat and centred, and try again.";
                return false;
            }

            Cap best = c1.Quality >= c2.Quality ? c1 : c2;
            quality = best.Quality;
            try
            {
                _repo.Save(pcno, fno, best.Iso, "ISO", best.Quality, "POC_ENROLL", by);
            }
            catch (Exception ex)
            {
                message = "Database error: " + ex.Message;
                return false;
            }
            message = EnrollRepository.FingerName(fno) + " enrolled for PC No " + pcno + " (quality " + quality + ", scans matched with score " + score + ").";
            return true;
        }

        public string ListJson(string pcno)
        {
            var sb = new System.Text.StringBuilder("{\"ok\":true,\"fingers\":[");
            List<EnrolledFinger> list;
            try { list = _repo.List((pcno ?? "").Trim()); }
            catch (Exception ex) { return "{\"ok\":false,\"message\":\"" + Json.Escape(ex.Message) + "\",\"fingers\":[]}"; }
            for (int i = 0; i < list.Count; i++)
            {
                EnrolledFinger f = list[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"finger\":").Append(f.FingerNo)
                  .Append(",\"name\":\"").Append(Json.Escape(EnrollRepository.FingerName(f.FingerNo)))
                  .Append("\",\"quality\":").Append(f.Quality)
                  .Append(",\"bytes\":").Append(f.Length)
                  .Append(",\"source\":\"").Append(Json.Escape(f.Source))
                  .Append("\",\"enrolledOn\":\"").Append(f.EnrolledOn.ToString("yyyy-MM-dd HH:mm")).Append("\"}");
            }
            return sb.Append("]}").ToString();
        }

        public string DeleteJson(string pcno, string finger)
        {
            int fno;
            if (!int.TryParse(finger, out fno)) return "{\"ok\":false,\"message\":\"Invalid finger\"}";
            try
            {
                int n = _repo.Delete((pcno ?? "").Trim(), fno);
                return "{\"ok\":" + (n > 0 ? "true" : "false") + ",\"message\":\"" + (n > 0 ? "Deleted" : "Not found") + "\"}";
            }
            catch (Exception ex) { return "{\"ok\":false,\"message\":\"" + Json.Escape(ex.Message) + "\"}"; }
        }

        // ------------------------------------------------------------------ console

        /// <summary>FingerprintVerifyPOC.exe --enroll : interactive enrolment in the console.</summary>
        public void RunConsole()
        {
            Console.WriteLine("Enrolling into table " + _repo.TableName + ". Fingers: 1 R-thumb 2 R-index 3 R-middle 4 R-ring 5 R-little, 6-10 = left hand.");
            while (true)
            {
                Console.WriteLine();
                Console.Write("PC No to enrol (or Q to quit): ");
                string pcno = (Console.ReadLine() ?? "q").Trim();
                if (pcno.Equals("q", StringComparison.OrdinalIgnoreCase)) return;
                Console.Write("Finger number 1-10 [2 = right index]: ");
                string finger = (Console.ReadLine() ?? "").Trim();
                if (finger.Length == 0) finger = "2";

                string t1, t2, msg;
                int q;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  >> Scan 1: place the finger when the light glows (" + _s.CaptureTimeoutSeconds + " s)");
                Console.ResetColor();
                if (!Capture(true, out t1, out q, out msg)) { Program.PrintLine(false, msg); continue; }
                Console.WriteLine("     " + msg + ". LIFT the finger...");
                Thread.Sleep(2000);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  >> Scan 2: place the SAME finger again");
                Console.ResetColor();
                if (!Capture(false, out t2, out q, out msg)) { Program.PrintLine(false, msg); continue; }

                int score, quality;
                bool ok = Enroll(pcno, finger, t1, t2, Environment.UserName, out score, out quality, out msg);
                Program.PrintLine(ok, msg);
            }
        }

        /// <summary>
        /// FingerprintVerifyPOC.exe --import [PCNO|ALL] : copies templates from the main view (ImportSourceTable)
        /// into the POC table. Reading goes through the normal decoder, so BLOB / Base64 / HEX sources all work.
        /// </summary>
        public static int Import(Settings s, EnrollRepository target, string onlyPcno)
        {
            var source = new FingerprintRepository(s, s.ImportSourceTable, s.ImportPcnoColumn, s.ImportTemplateColumn, s.ImportFingerColumn);
            if (string.Equals(source.TableName, target.TableName, StringComparison.OrdinalIgnoreCase))
            {
                Program.PrintLine(false, "ImportSourceTable and EnrollTable are the same table - nothing to do.");
                return 9;
            }
            Console.WriteLine("Importing from " + source.TableName + " into " + target.TableName +
                              (onlyPcno == null ? " (all PC Nos)" : " (PC No " + onlyPcno + ")") + " ...");

            int imported = 0, skipped = 0;
            var nextFinger = new Dictionary<string, int>();
            source.ReadAll(onlyPcno, (pcno, finger, data) =>
            {
                if (data == null || data.Length == 0 || !FingerprintRepository.StartsWithFmr(data))
                {
                    skipped++;
                    Console.WriteLine("  skip " + pcno + " finger " + finger + ": " + FingerprintRepository.Describe(data));
                    return;
                }
                int fno;
                if (!int.TryParse(finger, out fno) || fno < 1 || fno > 99)
                {
                    // no usable finger number in the source: number the rows of each PCNO 1, 2, 3...
                    int n;
                    nextFinger.TryGetValue(pcno, out n);
                    fno = ++n;
                    nextFinger[pcno] = n;
                }
                target.Save(pcno, fno, data, "AUTO", 0, "IMPORTED", Environment.UserName);
                imported++;
                if (imported % 100 == 0) Console.WriteLine("  " + imported + " imported...");
            });
            Program.PrintLine(true, "Import finished: " + imported + " templates imported, " + skipped + " skipped (not ISO/ANSI).");
            return 0;
        }
    }
}
