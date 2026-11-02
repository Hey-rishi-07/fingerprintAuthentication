using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using Oracle.ManagedDataAccess.Client;

namespace FingerprintVerifyPOC
{
    public class StoredTemplate
    {
        public string FingerLabel { get; set; }   // e.g. finger index / row id, only for logging
        public byte[] Data { get; set; }
    }

    /// <summary>
    /// Reads ALL enrolled templates for a PCNO from the configured Oracle view/table.
    /// Nothing is written to the database - the POC only needs SELECT rights on the view.
    /// </summary>
    public class FingerprintRepository
    {
        private static readonly Regex SafeIdentifier = new Regex(@"^[A-Za-z][A-Za-z0-9_$#]*(\.[A-Za-z][A-Za-z0-9_$#]*)?$");

        private readonly Settings _s;

        public FingerprintRepository(Settings s)
        {
            _s = s;
            // Table/column names come from config and are concatenated into SQL, so validate them.
            Check(_s.SourceTable, "FpSourceTable");
            Check(_s.PcnoColumn, "FpPcnoColumn");
            Check(_s.TemplateColumn, "FpTemplateColumn");
            if (!string.IsNullOrEmpty(_s.FingerColumn)) Check(_s.FingerColumn, "FpFingerColumn");
        }

        public bool TestConnection(out string message)
        {
            try
            {
                using (var conn = new OracleConnection(_s.ConnectionString))
                using (var cmd = conn.CreateCommand())
                {
                    conn.Open();
                    cmd.CommandText = "SELECT COUNT(*) FROM " + _s.SourceTable + " WHERE ROWNUM = 1";
                    cmd.ExecuteScalar();
                }
                message = "Oracle OK (" + _s.SourceTable + " is readable)";
                return true;
            }
            catch (Exception ex)
            {
                message = "Oracle connection failed: " + ex.Message;
                return false;
            }
        }

        public List<StoredTemplate> GetTemplates(string pcno)
        {
            var list = new List<StoredTemplate>();

            string sql = "SELECT " + _s.TemplateColumn +
                         (string.IsNullOrEmpty(_s.FingerColumn) ? "" : ", " + _s.FingerColumn) +
                         " FROM " + _s.SourceTable +
                         " WHERE " + _s.PcnoColumn + " = :pcno";

            using (var conn = new OracleConnection(_s.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                conn.Open();
                cmd.CommandText = sql;
                cmd.BindByName = true;
                cmd.InitialLOBFetchSize = -1;   // fetch full BLOB/CLOB with the row (templates are small)
                cmd.Parameters.Add(new OracleParameter("pcno", OracleDbType.Varchar2, pcno, ParameterDirection.Input));

                using (OracleDataReader r = cmd.ExecuteReader())
                {
                    int row = 0;
                    while (r.Read())
                    {
                        row++;
                        if (r.IsDBNull(0)) continue;
                        byte[] data = Decode(r.GetValue(0));
                        if (data == null || data.Length == 0) continue;

                        string label = string.IsNullOrEmpty(_s.FingerColumn) || r.IsDBNull(1)
                            ? "row" + row
                            : Convert.ToString(r.GetValue(1));
                        list.Add(new StoredTemplate { FingerLabel = label, Data = data });
                    }
                }
            }
            return list;
        }

        /// <summary>Handles BLOB/RAW (byte[]) as well as Base64/HEX text stored in CLOB/VARCHAR2.</summary>
        private byte[] Decode(object value)
        {
            byte[] bytes = value as byte[];
            string enc = (_s.TemplateEncoding ?? "AUTO").ToUpperInvariant();

            if (bytes != null)
            {
                if (enc == "BASE64" || enc == "HEX")
                    return DecodeText(System.Text.Encoding.ASCII.GetString(bytes), enc);
                return bytes; // RAW / AUTO
            }

            string text = Convert.ToString(value);
            return DecodeText(text, enc == "RAW" ? "AUTO" : enc);
        }

        private static byte[] DecodeText(string text, string enc)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            text = text.Trim();

            bool looksHex = text.Length % 2 == 0 && Regex.IsMatch(text, "^[0-9A-Fa-f]+$");
            if (enc == "HEX" || (enc == "AUTO" && looksHex))
            {
                var b = new byte[text.Length / 2];
                for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(text.Substring(i * 2, 2), 16);
                return b;
            }
            return Convert.FromBase64String(text);
        }

        private static void Check(string identifier, string key)
        {
            if (string.IsNullOrEmpty(identifier) || !SafeIdentifier.IsMatch(identifier))
                throw new ArgumentException("Invalid value for " + key + " in App.config: '" + identifier + "'");
        }
    }
}
