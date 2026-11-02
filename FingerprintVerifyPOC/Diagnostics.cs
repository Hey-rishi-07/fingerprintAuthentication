using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FingerprintVerifyPOC
{
    /// <summary>
    /// FingerprintVerifyPOC.exe --diag 123456
    /// Explains WHY matching fails: stored template format, live template format, matcher self-test,
    /// ISO vs ANSI scores against every stored finger, and a live-vs-live score for the same finger.
    /// Everything is also written to logs\diag_PCNO_time.txt so it can be shared.
    /// </summary>
    public static class Diagnostics
    {
        private static StringBuilder _report;

        public static int Run(string pcno, Settings s, Scanner scanner, FingerprintRepository repo, Verifier verifier)
        {
            _report = new StringBuilder();
            W("=== Fingerprint diagnostics  PCNO " + pcno + "  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
            W("Machine " + Environment.MachineName + ", 64-bit OS " + Environment.Is64BitOperatingSystem + ", 64-bit process " + Environment.Is64BitProcess);
            W("Config: source " + s.SourceTable + ", format " + s.TemplateFormat + ", encoding " + s.TemplateEncoding +
              ", threshold " + s.MatchThreshold + ", blink " + s.BlinkCount + "x (settle " + s.BlinkSettleMs + " ms), min quality " + s.MinQuality);

            // 1. Stored templates
            W("");
            W("[1] Stored templates in Oracle");
            List<StoredTemplate> stored;
            try { stored = repo.GetTemplates(pcno); }
            catch (Exception ex) { W("    DB ERROR: " + ex.Message); return Finish(pcno, s); }
            W("    rows found: " + stored.Count);
            foreach (StoredTemplate t in stored) W("    " + t.FingerLabel + ": " + FingerprintRepository.Describe(t.Data));
            if (stored.Count == 0) { W("    -> nothing to match. Check PCNO value / view."); return Finish(pcno, s); }

            // 2. Scanner
            W("");
            W("[2] Scanner");
            if (!scanner.EnsureInitialized()) { W("    NOT READY: " + scanner.LastError); return Finish(pcno, s); }
            W("    ready, model " + scanner.Model + ", serial " + scanner.SerialNo);

            // 3. First capture (with the configured blink)
            byte[] iso1, ansi1; int q1;
            W("");
            W("[3] Capture 1 - place the finger that is enrolled (light blinks " + s.BlinkCount + "x, then stays on)");
            scanner.Blink(s.BlinkCount, s.BlinkOnMs, s.BlinkOffMs, s.BlinkSettleMs);
            int ret = scanner.Capture(s.CaptureTimeoutSeconds * 1000, s.MinQuality, Console.WriteLine, out iso1, out ansi1, out q1);
            if (ret != 0) { W("    capture failed (" + ret + "): " + scanner.LastError); return Finish(pcno, s); }
            W("    quality " + q1);
            W("    live ISO : " + FingerprintRepository.Describe(iso1));
            W("    live ANSI: " + FingerprintRepository.Describe(ansi1));

            // 4. Matcher self-test
            W("");
            W("[4] Matcher self-test (live template against itself - must be a high score)");
            string err;
            int selfIso = scanner.Match(iso1, iso1, false, out err);
            W("    MatchISO  self score: " + selfIso + (err != null ? "  error: " + err : ""));
            int selfAnsi = ansi1 != null && ansi1.Length > 0 ? scanner.Match(ansi1, ansi1, true, out err) : -1;
            W("    MatchANSI self score: " + selfAnsi + (err != null ? "  error: " + err : ""));

            // 5. Live vs every stored finger, both methods
            W("");
            W("[5] Live finger vs stored fingers");
            int bestStored = -1;
            foreach (StoredTemplate t in stored)
            {
                string e1, e2;
                int a = scanner.Match(iso1, t.Data, false, out e1);
                int b = ansi1 != null && ansi1.Length > 0 ? scanner.Match(ansi1, t.Data, true, out e2) : -1;
                W("    " + t.FingerLabel + ":  MatchISO " + a + (e1 != null ? " (" + e1 + ")" : "") +
                  "   MatchANSI " + b);
                bestStored = Math.Max(bestStored, Math.Max(a, b));
            }

            // 6. Second capture of the same finger, without blink -> realistic genuine score for THIS matcher
            byte[] iso2, ansi2; int q2;
            W("");
            W("[6] Capture 2 - LIFT and place the SAME finger again (no blink)");
            ret = scanner.Capture(s.CaptureTimeoutSeconds * 1000, s.MinQuality, Console.WriteLine, out iso2, out ansi2, out q2);
            int liveLive = -1;
            if (ret == 0)
            {
                liveLive = scanner.Match(iso2, iso1, false, out err);
                W("    quality " + q2 + ";  capture1 vs capture2 MatchISO score: " + liveLive);
                foreach (StoredTemplate t in stored)
                {
                    string e1;
                    W("    capture2 vs " + t.FingerLabel + ": MatchISO " + scanner.Match(iso2, t.Data, false, out e1));
                }
            }
            else W("    capture 2 failed (" + ret + "): " + scanner.LastError);

            // 7. Interpretation
            W("");
            W("[7] What this means");
            bool anyFmr = false;
            foreach (StoredTemplate t in stored) if (FingerprintRepository.StartsWithFmr(t.Data)) anyFmr = true;
            if (!anyFmr)
                W("    * Stored data is NOT an ISO/ANSI template. The MFS100 SDK cannot match it. Find out which device/software\n" +
                  "      enrolled these fingerprints and in what format (or whether images are stored instead of templates).");
            if (selfIso <= 0)
                W("    * Matcher self-test failed -> SDK / DLL problem (check x86 DLLs, driver, licence of iengine_ansi_iso.dll).");
            if (liveLive > 0)
                W("    * Same finger live-vs-live scored " + liveLive + ". A genuine match should score in that range;" +
                  " set MatchThreshold clearly below it and above wrong-finger scores.");
            if (liveLive > 0 && bestStored >= 0 && bestStored < liveLive / 4 && anyFmr)
                W("    * Stored templates score far lower than a live re-capture -> they were probably enrolled with a different\n" +
                  "      device/algorithm, a different finger, or are damaged/truncated. Try other fingers; compare template sizes.");
            if (bestStored >= s.MatchThreshold)
                W("    * Best stored score " + bestStored + " >= threshold " + s.MatchThreshold + " -> this PCNO WOULD verify.");
            else
                W("    * Best stored score " + bestStored + " < threshold " + s.MatchThreshold + " -> this PCNO would NOT verify.");
            return Finish(pcno, s);
        }

        private static int Finish(string pcno, Settings s)
        {
            try
            {
                string dir = Path.IsPathRooted(s.LogFolder) ? s.LogFolder : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, s.LogFolder);
                Directory.CreateDirectory(dir);
                string safe = string.Join("_", pcno.Split(Path.GetInvalidFileNameChars()));
                string file = Path.Combine(dir, "diag_" + safe + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
                File.WriteAllText(file, _report.ToString());
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Report saved: " + file);
                Console.ResetColor();
            }
            catch { }
            return 0;
        }

        private static void W(string line)
        {
            Console.WriteLine(line);
            _report.AppendLine(line);
        }
    }
}
