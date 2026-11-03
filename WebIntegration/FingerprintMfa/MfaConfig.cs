using System;
using System.Configuration;
using System.Linq;
using System.Text.RegularExpressions;

namespace FingerprintMfa
{
    /// <summary>All settings come from the web application's Web.config (keys starting with "FpMfa.").</summary>
    public sealed class MfaConfig
    {
        private static MfaConfig _current;
        public static MfaConfig Current { get { return _current ?? (_current = Load()); } }

        public bool Enabled;                 // master switch
        public string ConnectionString;
        public string SourceTable, PcnoColumn, TemplateColumn;
        public string TemplateFormat;        // AUTO | ISO | ANSI
        public string TemplateEncoding;      // AUTO | RAW | BASE64 | HEX
        public int MatchThreshold;
        public int MaxAttempts;
        public int PendingMinutes;
        public bool RequireForAllUsers;
        public bool AllowIfNotEnrolled;
        public string SignInMode;            // Forms | Session | None
        public string SessionUserKey;
        public string LoginUrl, DefaultUrl, VerifyUrl;
        public string[] AdminUsers;
        public string AgentUrl;
        public string AgentDownloadUrl;
        public int CaptureTimeoutSeconds;
        public string MantraFolder;
        public string AppName;
        public string UsersTable, AuditTable, AuditSequence;

        public bool TryIso { get { return !TemplateFormat.Equals("ANSI", StringComparison.OrdinalIgnoreCase); } }
        public bool TryAnsi { get { return !TemplateFormat.Equals("ISO", StringComparison.OrdinalIgnoreCase); } }

        private static MfaConfig Load()
        {
            string csName = S("ConnectionName", "OracleDB");
            var cs = ConfigurationManager.ConnectionStrings[csName];
            var c = new MfaConfig
            {
                Enabled = B("Enabled", true),
                ConnectionString = cs != null ? cs.ConnectionString : null,
                SourceTable = Id(S("SourceTable", "FP_AUTH_V")),
                PcnoColumn = Id(S("PcnoColumn", "PCNO")),
                TemplateColumn = Id(S("TemplateColumn", "FINGER_TEMPLATE")),
                TemplateFormat = S("TemplateFormat", "AUTO"),
                TemplateEncoding = S("TemplateEncoding", "AUTO"),
                MatchThreshold = I("MatchThreshold", 1400),
                MaxAttempts = I("MaxAttempts", 3),
                PendingMinutes = I("PendingMinutes", 5),
                RequireForAllUsers = B("RequireForAllUsers", false),
                AllowIfNotEnrolled = B("AllowIfNotEnrolled", false),
                SignInMode = S("SignInMode", "Forms"),
                SessionUserKey = S("SessionUserKey", "PCNO"),
                LoginUrl = S("LoginUrl", "~/Login.aspx"),
                DefaultUrl = S("DefaultUrl", "~/Default.aspx"),
                VerifyUrl = S("VerifyUrl", "~/FingerprintVerify.aspx"),
                AdminUsers = S("AdminUsers", "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                                .Select(a => a.Trim()).Where(a => a.Length > 0).ToArray(),
                AgentUrl = S("AgentUrl", "http://localhost:8036").TrimEnd('/'),
                AgentDownloadUrl = S("AgentDownloadUrl", ""),
                CaptureTimeoutSeconds = I("CaptureTimeoutSeconds", 10),
                MantraFolder = S("MantraFolder", "~/bin/mantra"),
                AppName = S("AppName", "WebApp"),
                UsersTable = Id(S("UsersTable", "FP_MFA_USERS")),
                AuditTable = Id(S("AuditTable", "FP_MFA_AUDIT")),
                AuditSequence = Id(S("AuditSequence", "FP_MFA_AUDIT_SEQ"))
            };
            if (c.Enabled && string.IsNullOrEmpty(c.ConnectionString))
                throw new ConfigurationErrorsException("FingerprintMfa: connection string '" + csName + "' not found in Web.config.");
            return c;
        }

        private static readonly Regex SafeId = new Regex(@"^[A-Za-z][A-Za-z0-9_$#]*(\.[A-Za-z][A-Za-z0-9_$#]*)?$");

        /// <summary>Table/column names are put into SQL text, so only plain identifiers are accepted.</summary>
        private static string Id(string v)
        {
            if (!SafeId.IsMatch(v ?? "")) throw new ConfigurationErrorsException("FingerprintMfa: invalid table/column name '" + v + "' in Web.config.");
            return v;
        }

        private static string S(string k, string d) { string v = ConfigurationManager.AppSettings["FpMfa." + k]; return string.IsNullOrWhiteSpace(v) ? d : v.Trim(); }
        private static int I(string k, int d) { int v; return int.TryParse(S(k, ""), out v) ? v : d; }
        private static bool B(string k, bool d) { string v = S(k, ""); return v.Length == 0 ? d : v.Equals("true", StringComparison.OrdinalIgnoreCase); }
    }
}
