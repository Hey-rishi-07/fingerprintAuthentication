using System;
using System.Collections.Generic;
using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace FingerprintMfa
{
    public class MfaUser
    {
        public string Pcno;
        public bool Enabled;
        public string Remarks;
        public string UpdatedBy;
        public DateTime? UpdatedOn;
        public int Fingers;
    }

    public class MfaAuditRow
    {
        public DateTime Time;
        public string Pcno, Event, Result, ClientIp, App, Message;
        public int Score;
    }

    /// <summary>Oracle access (11g R2+). All values are bind variables (p_ prefix); names come from validated config.</summary>
    internal class MfaRepository
    {
        private readonly MfaConfig _c;

        public MfaRepository(MfaConfig c) { _c = c; }

        private OracleConnection Open()
        {
            var conn = new OracleConnection(_c.ConnectionString);
            conn.Open();
            return conn;
        }

        private static OracleParameter P(string name, object value)
        {
            return new OracleParameter(name, value ?? DBNull.Value);
        }

        // ------------------------------------------------------------------ enrolled templates (read only)

        public List<byte[]> GetTemplates(string pcno)
        {
            var list = new List<byte[]>();
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.BindByName = true;
                cmd.InitialLOBFetchSize = -1;
                cmd.CommandText = "SELECT " + _c.TemplateColumn + " FROM " + _c.SourceTable + " WHERE " + _c.PcnoColumn + " = :p_pcno";
                cmd.Parameters.Add(P("p_pcno", pcno));
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        if (r.IsDBNull(0)) continue;
                        byte[] t = null;
                        try { t = TemplateCodec.Decode(r.GetValue(0), _c.TemplateEncoding); } catch { }
                        if (t != null && t.Length > 0) list.Add(t);
                    }
                }
            }
            return list;
        }

        public int CountTemplates(string pcno)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.BindByName = true;
                cmd.CommandText = "SELECT COUNT(*) FROM " + _c.SourceTable + " WHERE " + _c.PcnoColumn + " = :p_pcno";
                cmd.Parameters.Add(P("p_pcno", pcno));
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        // ------------------------------------------------------------------ per-user switch

        /// <returns>true/false if the user has a row, null if not listed</returns>
        public bool? GetUserEnabled(string pcno)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.BindByName = true;
                cmd.CommandText = "SELECT MFA_ENABLED FROM " + _c.UsersTable + " WHERE PCNO = :p_pcno";
                cmd.Parameters.Add(P("p_pcno", pcno));
                object v = cmd.ExecuteScalar();
                if (v == null || v == DBNull.Value) return null;
                return Convert.ToString(v) == "Y";
            }
        }

        public void SetUser(string pcno, bool enabled, string remarks, string by)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.BindByName = true;
                cmd.CommandText =
                    "MERGE INTO " + _c.UsersTable + " t USING (SELECT :p_pcno PCNO FROM DUAL) s ON (t.PCNO = s.PCNO) " +
                    "WHEN MATCHED THEN UPDATE SET t.MFA_ENABLED = :p_flag, t.REMARKS = NVL(:p_rem, t.REMARKS), " +
                    "  t.UPDATED_BY = :p_upd, t.UPDATED_ON = SYSDATE " +
                    "WHEN NOT MATCHED THEN INSERT (PCNO, MFA_ENABLED, REMARKS, UPDATED_BY, UPDATED_ON) " +
                    "  VALUES (:p_pcno, :p_flag, :p_rem, :p_upd, SYSDATE)";
                cmd.Parameters.Add(P("p_pcno", pcno));
                cmd.Parameters.Add(P("p_flag", enabled ? "Y" : "N"));
                cmd.Parameters.Add(P("p_rem", string.IsNullOrEmpty(remarks) ? null : remarks));
                cmd.Parameters.Add(P("p_upd", by));
                cmd.ExecuteNonQuery();
            }
        }

        public List<MfaUser> ListUsers(string search)
        {
            var list = new List<MfaUser>();
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.BindByName = true;
                cmd.CommandText =
                    "SELECT * FROM (SELECT u.PCNO, u.MFA_ENABLED, u.REMARKS, u.UPDATED_BY, u.UPDATED_ON, " +
                    "  (SELECT COUNT(*) FROM " + _c.SourceTable + " s WHERE s." + _c.PcnoColumn + " = u.PCNO) FINGERS " +
                    "  FROM " + _c.UsersTable + " u WHERE (:p_s IS NULL OR u.PCNO LIKE :p_s) ORDER BY u.UPDATED_ON DESC) " +
                    "WHERE ROWNUM <= 500";
                cmd.Parameters.Add(P("p_s", string.IsNullOrWhiteSpace(search) ? null : "%" + search.Trim() + "%"));
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new MfaUser
                        {
                            Pcno = Convert.ToString(r.GetValue(0)),
                            Enabled = Convert.ToString(r.GetValue(1)) == "Y",
                            Remarks = r.IsDBNull(2) ? "" : Convert.ToString(r.GetValue(2)),
                            UpdatedBy = r.IsDBNull(3) ? "" : Convert.ToString(r.GetValue(3)),
                            UpdatedOn = r.IsDBNull(4) ? (DateTime?)null : Convert.ToDateTime(r.GetValue(4)),
                            Fingers = Convert.ToInt32(r.GetValue(5))
                        });
                    }
                }
            }
            return list;
        }

        // ------------------------------------------------------------------ audit

        public void Audit(string pcno, string evt, string result, int score, int quality, string ip, string message)
        {
            try
            {
                using (var conn = Open())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.BindByName = true;
                    cmd.CommandText =
                        "INSERT INTO " + _c.AuditTable + " (AUDIT_ID, PCNO, EVENT, RESULT, SCORE, QUALITY, CLIENT_IP, APP_NAME, MESSAGE, CREATED_ON) " +
                        "VALUES (" + _c.AuditSequence + ".NEXTVAL, :p_pcno, :p_evt, :p_res, :p_score, :p_q, :p_ip, :p_app, :p_msg, SYSDATE)";
                    cmd.Parameters.Add(P("p_pcno", Cut(pcno, 30)));
                    cmd.Parameters.Add(P("p_evt", Cut(evt, 30)));
                    cmd.Parameters.Add(P("p_res", Cut(result, 20)));
                    cmd.Parameters.Add(P("p_score", score));
                    cmd.Parameters.Add(P("p_q", quality));
                    cmd.Parameters.Add(P("p_ip", Cut(ip, 50)));
                    cmd.Parameters.Add(P("p_app", Cut(_c.AppName, 50)));
                    cmd.Parameters.Add(P("p_msg", Cut(message, 400)));
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("FingerprintMfa audit failed: " + ex.Message);
            }
        }

        public List<MfaAuditRow> RecentAudit(string pcno, int max)
        {
            var list = new List<MfaAuditRow>();
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.BindByName = true;
                cmd.CommandText =
                    "SELECT * FROM (SELECT CREATED_ON, PCNO, EVENT, RESULT, NVL(SCORE,0), CLIENT_IP, APP_NAME, MESSAGE FROM " + _c.AuditTable +
                    " WHERE (:p_pcno IS NULL OR PCNO = :p_pcno) ORDER BY AUDIT_ID DESC) WHERE ROWNUM <= :p_max";
                cmd.Parameters.Add(P("p_pcno", string.IsNullOrWhiteSpace(pcno) ? null : pcno.Trim()));
                cmd.Parameters.Add(P("p_max", max));
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new MfaAuditRow
                        {
                            Time = Convert.ToDateTime(r.GetValue(0)),
                            Pcno = Convert.ToString(r.GetValue(1)),
                            Event = Convert.ToString(r.GetValue(2)),
                            Result = Convert.ToString(r.GetValue(3)),
                            Score = Convert.ToInt32(r.GetValue(4)),
                            ClientIp = Convert.ToString(r.GetValue(5)),
                            App = Convert.ToString(r.GetValue(6)),
                            Message = Convert.ToString(r.GetValue(7))
                        });
                    }
                }
            }
            return list;
        }

        private static string Cut(string s, int n)
        {
            if (s == null) return null;
            return s.Length <= n ? s : s.Substring(0, n);
        }
    }
}
