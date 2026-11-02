using System;
using System.Threading;

namespace FingerprintVerifyPOC
{
    /// <summary>
    /// Usage:
    ///   FingerprintVerifyPOC.exe                 -> web mode: local page at http://localhost:8035/ (default)
    ///   FingerprintVerifyPOC.exe --no-browser    -> web mode without opening the browser (used at logon)
    ///   FingerprintVerifyPOC.exe --console       -> interactive console: type PCNO, verify, repeat
    ///   FingerprintVerifyPOC.exe --diag 123456   -> explains why matching fails, saves logs\diag_*.txt
    ///   FingerprintVerifyPOC.exe --pcno 123456   -> one verification, prints JSON, exit code = result
    ///
    /// Exit codes (for --pcno): 0 VERIFIED, 1 NOT_MATCHED, 2 TIMEOUT, 3 NOT_ENROLLED,
    ///                          4 DEVICE_ERROR, 5 DB_ERROR, 6 INVALID_PCNO, 9 startup error
    /// </summary>
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            Console.Title = "Fingerprint Verification POC";
            string pcnoArg = null;
            bool consoleMode = false;
            bool noBrowser = false;
            string diagPcno = null;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].ToLowerInvariant();
                if (a == "--console" || a == "-c") consoleMode = true;
                else if (a == "--no-browser") noBrowser = true;
                else if (a == "--diag") diagPcno = (i + 1 < args.Length) ? args[++i] : "";
                else if ((a == "--pcno" || a == "-p") && i + 1 < args.Length) pcnoArg = args[++i];
            }

            Settings s;
            FingerprintRepository repo;
            try
            {
                s = Settings.Load();
                repo = new FingerprintRepository(s);
            }
            catch (Exception ex)
            {
                Error("Configuration error: " + ex.Message);
                return 9;
            }

            using (var scanner = new Scanner())
            {
                var verifier = new Verifier(s, scanner, repo);

                if (pcnoArg != null)
                {
                    // Scripted / automated single verification
                    VerifyResult r = verifier.Verify(pcnoArg, (stage, msg) => Console.Error.WriteLine(stage + ": " + msg));
                    Console.WriteLine(r.ToJson());
                    try { Oracle.ManagedDataAccess.Client.OracleConnection.ClearAllPools(); } catch { }
                    Console.Out.Flush();
                    Environment.Exit((int)r.Status);   // hard exit so pool threads can't change the exit code
                    return (int)r.Status;
                }

                Banner(s, scanner, repo);

                if (diagPcno != null)
                {
                    if (diagPcno.Length == 0)
                    {
                        Console.Write("Enter PC No to diagnose: ");
                        diagPcno = (Console.ReadLine() ?? "").Trim();
                    }
                    Diagnostics.Run(diagPcno, s, scanner, repo, verifier);
                    Console.WriteLine("Press any key to exit...");
                    try { Console.ReadKey(); } catch { }
                    return 0;
                }

                if (consoleMode)
                {
                    RunConsole(verifier);
                    return 0;
                }

                var web = new WebHost(s, verifier, scanner, repo);
                if (s.OpenBrowser && !noBrowser)
                {
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        Thread.Sleep(700);
                        try { System.Diagnostics.Process.Start(web.BaseUrl); } catch { }
                    });
                }
                try
                {
                    web.Run();
                }
                catch (Exception ex)
                {
                    Error(ex.Message);
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    return 9;
                }
                return 0;
            }
        }

        private static void RunConsole(Verifier verifier)
        {
            while (true)
            {
                Console.WriteLine();
                Console.Write("Enter PC No (or Q to quit): ");
                string pcno = Console.ReadLine();
                if (pcno == null || pcno.Trim().Equals("q", StringComparison.OrdinalIgnoreCase)) return;
                if (pcno.Trim().Length == 0) continue;

                Timer countdown = null;
                int left = 0;
                VerifyResult r = verifier.Verify(pcno, (stage, msg) =>
                {
                    if (stage == "WAITING")
                    {
                        left = int.Parse(System.Text.RegularExpressions.Regex.Match(msg, @"\d+").Value);
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("  >> Scanner light ON. " + msg);
                        Console.ResetColor();
                        countdown = new Timer(_ =>
                        {
                            if (left > 0) Console.Write("\r     waiting for finger... " + left-- + "s ");
                        }, null, 0, 1000);
                    }
                    else
                    {
                        Console.WriteLine("  .. " + msg);
                    }
                });
                if (countdown != null) { countdown.Dispose(); Console.WriteLine("\r                                   "); }
                PrintResult(r);
            }
        }

        public static void PrintResult(VerifyResult r)
        {
            ConsoleColor c = r.Verified ? ConsoleColor.Green
                           : r.Status == VerifyStatus.NOT_MATCHED ? ConsoleColor.Red
                           : ConsoleColor.Yellow;
            Console.ForegroundColor = c;
            Console.WriteLine("  ==========================================================");
            Console.WriteLine("   " + (r.Verified ? "AUTHENTICATION SUCCESSFUL" :
                                       r.Status == VerifyStatus.NOT_MATCHED ? "AUTHENTICATION FAILED" : r.Status.ToString()) +
                              "   PCNO " + r.Pcno);
            Console.WriteLine("   " + r.Message);
            if (r.TemplatesChecked > 0)
                Console.WriteLine("   Score " + r.BestScore + " (threshold " + r.Threshold + "), quality " + r.Quality +
                                  ", enrolled fingers checked " + r.TemplatesChecked);
            Console.WriteLine("  ==========================================================");
            Console.ResetColor();
            try
            {
                if (r.Verified) Console.Beep(1200, 180);
                else if (r.Status == VerifyStatus.NOT_MATCHED) { Console.Beep(400, 250); Console.Beep(400, 250); }
            }
            catch { }
        }

        private static void Banner(Settings s, Scanner scanner, FingerprintRepository repo)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=============================================================");
            Console.WriteLine("  Fingerprint Verification POC  (Mantra MFS100 + Oracle)");
            Console.WriteLine("=============================================================");
            Console.ResetColor();

            bool dev = scanner.EnsureInitialized();
            Status(dev, dev ? "Scanner ready " + (scanner.Model ?? "") + " " + (scanner.SerialNo ?? "") : "Scanner: " + scanner.LastError);

            string dbMsg;
            bool db = repo.TestConnection(out dbMsg);
            Status(db, dbMsg);

            Console.WriteLine("  Source: " + s.SourceTable + "  (" + s.PcnoColumn + ", " + s.TemplateColumn + ", " +
                              s.TemplateFormat + ")   Threshold: " + s.MatchThreshold + "   Timeout: " + s.CaptureTimeoutSeconds + "s");
        }

        private static void Status(bool ok, string text)
        {
            Console.ForegroundColor = ok ? ConsoleColor.Green : ConsoleColor.Yellow;
            Console.WriteLine("  [" + (ok ? " OK " : "WARN") + "] " + text);
            Console.ResetColor();
        }

        private static void Error(string text)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(text);
            Console.ResetColor();
        }
    }
}
