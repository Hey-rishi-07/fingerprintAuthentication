using System;
using System.Collections.Generic;
using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace FingerprintVerifyPOC
{
    public class EnrolledFinger
    {
        public int FingerNo;
        public int Quality;
        public int Length;
        public string Source;
        public DateTime EnrolledOn;
    }

    /// <summary>
    /// The POC's OWN enrolment table (default FP_POC_ENROLL). The main fingerprint table is never written to.
    /// Oracle 11g compatible SQL (no IDENTITY columns, MERGE for upsert).
    /// Columns are PCNO / FINGER_NO / FINGER_TEMPLATE, so the same table can be used for verification
    /// simply by setting FpSourceTable = FP_POC_ENROLL.
    /// </summary>
    public class EnrollRepository
    {
        private readonly Settings _s;
        private readonly string _table;

        public EnrollRepository(Settings s)
        {
            _s = s;
            _table = s.EnrollTable;
            if (!System.Text.RegularExpressions.Regex.IsMatch(_table ?? "", @"^[A-Za-z][A-Za-z0-9_$#]*(\.[A-Za-z][A-Za-z0-9_$#]*)?$"))
                throw new ArgumentException("Invalid EnrollTable in App.config: '" + _table + "'");
        }

        public string TableName { get { return _table; } }

        public static string FingerName(int n)
        {
            string[] names = { "", "Right thumb", "Right index", "Right middle", "Right ring", "Right little",
                               "Left thumb", "Left index", "Left middle", "Left ring", "Left little" };
            return n >= 1 && n <= 10 ? names[n] : "Finger " + n;
        }

        /// <summary>Creates the table if it does not exist (needs CREATE TABLE privilege; otherwise run Sql\Create_POC_Enroll_Table.sql).</summary>
        public void EnsureTable()
        {
            using (var conn = Open())
            {
                using (var cmd = conn.CreateCommand())
                {
                    string owner = null, name = _table.ToUpperInvariant();
                    int dot = name.IndexOf('.');
                    if (dot > 0) { owner = name.Substring(0, dot); name = name.Substring(dot + 1); }
                    cmd.BindByName = true;
                    cmd.CommandText = owner == null
                        ? "SELECT COUNT(*) FROM USER_TABLES WHERE TABLE_NAME = :p_t"
                        : "SELECT COUNT(*) FROM ALL_TABLES WHERE TABLE_NAME = :p_t AND OWNER = :p_o";
                    cmd.Parameters.Add(new OracleParameter("p_t", name));
                    if (owner != null) cmd.Parameters.Add(new OracleParameter("p_o", owner));
                    if (Convert.ToInt32(cmd.ExecuteScalar()) > 0) return;
                }
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        "CREATE TABLE " + _table + " (" +
                        " PCNO VARCHAR2(30) NOT NULL," +
                        " FINGER_NO NUMBER(2) NOT NULL," +
                        " FINGER_TEMPLATE BLOB NOT NULL," +
                        " TEMPLATE_FORMAT VARCHAR2(10) DEFAULT 'ISO'," +
                        " QUALITY NUMBER(3)," +
                        " SOURCE VARCHAR2(20) DEFAULT 'POC_ENROLL'," +
                        " ENROLLED_ON DATE DEFAULT SYSDATE," +
                        " ENROLLED_BY VARCHAR2(100)," +
                        " CONSTRAINT " + ShortName() + "_PK PRIMARY KEY (PCNO, FINGER_NO))";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>Insert or replace one finger of one PCNO.</summary>
        public void Save(string pcno, int fingerNo, byte[] template, string format, int quality, string source, string by)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.BindByName = true;
                cmd.CommandText =
                    "MERGE INTO " + _table + " t USING (SELECT :p_pcno PCNO, :p_fno FINGER_NO FROM DUAL) s " +
                    "ON (t.PCNO = s.PCNO AND t.FINGER_NO = s.FINGER_NO) " +
                    "WHEN MATCHED THEN UPDATE SET t.FINGER_TEMPLATE = :p_tmpl, t.TEMPLATE_FORMAT = :p_fmt, t.QUALITY = :p_q, " +
                    "  t.SOURCE = :p_src, t.ENROLLED_ON = SYSDATE, t.ENROLLED_BY = :p_by " +
                    "WHEN NOT MATCHED THEN INSERT (PCNO, FINGER_NO, FINGER_TEMPLATE, TEMPLATE_FORMAT, QUALITY, SOURCE, ENROLLED_ON, ENROLLED_BY) " +
                    "  VALUES (:p_pcno, :p_fno, :p_tmpl, :p_fmt, :p_q, :p_src, SYSDATE, :p_by)";
                cmd.Parameters.Add(new OracleParameter("p_pcno", OracleDbType.Varchar2, pcno, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter("p_fno", OracleDbType.Int32, fingerNo, ParameterDirection.Input));
                var blob = new OracleParameter("p_tmpl", OracleDbType.Blob);
                blob.Value = template;
                cmd.Parameters.Add(blob);
                cmd.Parameters.Add(new OracleParameter("p_fmt", OracleDbType.Varchar2, format, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter("p_q", OracleDbType.Int32, quality, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter("p_src", OracleDbType.Varchar2, source, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter("p_by", OracleDbType.Varchar2, by ?? "", ParameterDirection.Input));
                cmd.ExecuteNonQuery();
            }
        }

        public List<EnrolledFinger> List(string pcno)
        {
            var list = new List<EnrolledFinger>();
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.BindByName = true;
                cmd.CommandText = "SELECT FINGER_NO, NVL(QUALITY,0), DBMS_LOB.GETLENGTH(FINGER_TEMPLATE), NVL(SOURCE,' '), ENROLLED_ON " +
                                  "FROM " + _table + " WHERE PCNO = :p_pcno ORDER BY FINGER_NO";
                cmd.Parameters.Add(new OracleParameter("p_pcno", OracleDbType.Varchar2, pcno, ParameterDirection.Input));
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new EnrolledFinger
                        {
                            FingerNo = Convert.ToInt32(r.GetValue(0)),
                            Quality = Convert.ToInt32(r.GetValue(1)),
                            Length = r.IsDBNull(2) ? 0 : Convert.ToInt32(r.GetValue(2)),
                            Source = Convert.ToString(r.GetValue(3)).Trim(),
                            EnrolledOn = r.IsDBNull(4) ? DateTime.MinValue : Convert.ToDateTime(r.GetValue(4))
                        });
                    }
                }
            }
            return list;
        }

        public int Delete(string pcno, int fingerNo)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.BindByName = true;
                cmd.CommandText = "DELETE FROM " + _table + " WHERE PCNO = :p_pcno AND FINGER_NO = :p_fno";
                cmd.Parameters.Add(new OracleParameter("p_pcno", OracleDbType.Varchar2, pcno, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter("p_fno", OracleDbType.Int32, fingerNo, ParameterDirection.Input));
                return cmd.ExecuteNonQuery();
            }
        }

        private OracleConnection Open()
        {
            var c = new OracleConnection(_s.ConnectionString);
            c.Open();
            return c;
        }

        private string ShortName()
        {
            string n = _table.Contains(".") ? _table.Substring(_table.IndexOf('.') + 1) : _table;
            return n.Length > 26 ? n.Substring(0, 26) : n;   // Oracle 11g: identifiers max 30 chars
        }
    }
}
