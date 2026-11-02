using System;
using System.Configuration;

namespace FingerprintVerifyPOC
{
    /// <summary>All tunables live in FingerprintVerifyPOC.exe.config (App.config) so nothing needs recompiling.</summary>
    public class Settings
    {
        public string ConnectionString;
        public string SourceTable;
        public string PcnoColumn;
        public string TemplateColumn;
        public string FingerColumn;
        public string TemplateFormat;     // AUTO | ISO | ANSI
        public string TemplateEncoding;   // AUTO | RAW | BASE64 | HEX

        public int MatchThreshold;
        public int CaptureTimeoutSeconds;
        public int MinQuality;
        public int BlinkCount;
        public int BlinkOnMs;
        public int BlinkOffMs;
        public string PcnoPattern;

        public int HttpPort;
        public string AllowedOrigins;
        public bool OpenBrowser;
        public string LogFolder;

        public int BlinkSettleMs;

        public bool TryIso { get { return !string.Equals(TemplateFormat, "ANSI", StringComparison.OrdinalIgnoreCase); } }
        public bool TryAnsi { get { return !string.Equals(TemplateFormat, "ISO", StringComparison.OrdinalIgnoreCase); } }

        public static Settings Load()
        {
            var cs = ConfigurationManager.ConnectionStrings["OracleDB"];
            return new Settings
            {
                ConnectionString = cs != null ? cs.ConnectionString : "",
                SourceTable = Str("FpSourceTable", "FP_AUTH_V"),
                PcnoColumn = Str("FpPcnoColumn", "PCNO"),
                TemplateColumn = Str("FpTemplateColumn", "FINGER_TEMPLATE"),
                FingerColumn = Str("FpFingerColumn", ""),
                TemplateFormat = Str("TemplateFormat", "AUTO"),
                TemplateEncoding = Str("TemplateEncoding", "AUTO"),
                MatchThreshold = Int("MatchThreshold", 1400),
                CaptureTimeoutSeconds = Int("CaptureTimeoutSeconds", 10),
                MinQuality = Int("MinQuality", 40),
                BlinkCount = Int("BlinkCount", 3),
                BlinkOnMs = Int("BlinkOnMs", 250),
                BlinkOffMs = Int("BlinkOffMs", 250),
                BlinkSettleMs = Int("BlinkSettleMs", 700),
                PcnoPattern = Str("PcnoPattern", "^[A-Za-z0-9_-]{1,30}$"),
                HttpPort = Int("HttpPort", 8035),
                AllowedOrigins = Str("AllowedOrigins", "*"),
                OpenBrowser = Str("OpenBrowser", "true").Equals("true", StringComparison.OrdinalIgnoreCase),
                LogFolder = Str("LogFolder", "logs")
            };
        }

        private static string Str(string key, string def)
        {
            string v = ConfigurationManager.AppSettings[key];
            return v == null ? def : v.Trim();
        }

        private static int Int(string key, int def)
        {
            int v;
            return int.TryParse(ConfigurationManager.AppSettings[key], out v) ? v : def;
        }
    }
}
