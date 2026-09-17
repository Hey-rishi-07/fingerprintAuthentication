using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace CentralMFA.Utils
{
    public class RegisteredApp
    {
        public string AppId { get; set; }
        public string AppName { get; set; }
        public string BaseUrl { get; set; }
        public string CallbackUrl { get; set; }
        public string SecretKey { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedOn { get; set; }
        public string PolicyType { get; set; } // Joined from MFA_APP_POLICIES
    }

    public class AuditLogEntry
    {
        public long LogId { get; set; }
        public string AppId { get; set; }
        public string PcNo { get; set; }
        public string ActionName { get; set; }
        public string TxnId { get; set; }
        public int MatchScore { get; set; }
        public string Status { get; set; }
        public string ClientIp { get; set; }
        public string Details { get; set; }
        public DateTime CreatedOn { get; set; }
    }

    public class DashboardStats
    {
        public int TotalApps { get; set; }
        public int ActiveApps { get; set; }
        public int TotalEnrolledUsers { get; set; }
        public int VerificationsToday { get; set; }
        public int ApprovedToday { get; set; }
        public int DeniedToday { get; set; }
    }

    public class EnrolledUser
    {
        public string PcNo { get; set; }
        public int TemplateLength { get; set; }
        public int Quality { get; set; }
        public DateTime UpdatedOn { get; set; }
    }

    public static class DBHelper
    {
        private static string GetConnectionString()
        {
            var cs = ConfigurationManager.ConnectionStrings["OracleDB"]
                  ?? ConfigurationManager.ConnectionStrings["AttendanceDB"];
            if (cs == null || string.IsNullOrWhiteSpace(cs.ConnectionString))
            {
                return "User Id=system;Password=root;Data Source=//127.0.0.1:1521/xe;";
            }
            return cs.ConnectionString;
        }

        public static OracleConnection GetConnection()
        {
            return new OracleConnection(GetConnectionString());
        }

        public static void EnsureSchemaExists()
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    // 1. Table APP_USER_FINGERPRINTS
                    if (!TableExists(conn, "APP_USER_FINGERPRINTS"))
                    {
                        ExecuteSql(conn, @"
                            CREATE TABLE APP_USER_FINGERPRINTS (
                                PCNO            VARCHAR2(50)   NOT NULL PRIMARY KEY,
                                FINGER_TEMPLATE BLOB           NOT NULL,
                                QUALITY         NUMBER(3)      DEFAULT 0,
                                CREATED_ON      DATE           DEFAULT SYSDATE,
                                UPDATED_ON      DATE           DEFAULT SYSDATE
                            )");
                    }

                    // 2. Table MFA_REGISTERED_APPS
                    if (!TableExists(conn, "MFA_REGISTERED_APPS"))
                    {
                        ExecuteSql(conn, @"
                            CREATE TABLE MFA_REGISTERED_APPS (
                                APP_ID          VARCHAR2(50)   NOT NULL PRIMARY KEY,
                                APP_NAME        VARCHAR2(100)  NOT NULL,
                                BASE_URL        VARCHAR2(255),
                                CALLBACK_URL    VARCHAR2(255)  NOT NULL,
                                SECRET_KEY      VARCHAR2(64)   NOT NULL,
                                IS_ACTIVE       NUMBER(1)      DEFAULT 1,
                                CREATED_ON      DATE           DEFAULT SYSDATE
                            )");

                        // Seed default apps
                        ExecuteSql(conn, "INSERT INTO MFA_REGISTERED_APPS (APP_ID, APP_NAME, BASE_URL, CALLBACK_URL, SECRET_KEY, IS_ACTIVE) VALUES ('HRMS', 'HR Management System', 'http://127.0.0.1:5100/hrms/', 'http://127.0.0.1:5100/hrms/Callback.aspx', 'HRMS_SECRET_KEY_8F9A2B1C', 1)");
                        ExecuteSql(conn, "INSERT INTO MFA_REGISTERED_APPS (APP_ID, APP_NAME, BASE_URL, CALLBACK_URL, SECRET_KEY, IS_ACTIVE) VALUES ('ERP', 'Enterprise Resource Planning', 'http://127.0.0.1:5200/erp/', 'http://127.0.0.1:5200/erp/Callback.aspx', 'ERP_SECRET_KEY_77B1C9D2', 1)");
                        ExecuteSql(conn, "INSERT INTO MFA_REGISTERED_APPS (APP_ID, APP_NAME, BASE_URL, CALLBACK_URL, SECRET_KEY, IS_ACTIVE) VALUES ('PAYROLL', 'Payroll Processing System', 'http://127.0.0.1:5300/payroll/', 'http://127.0.0.1:5300/payroll/Callback.aspx', 'PAYROLL_SECRET_KEY_55E3F8A1', 1)");
                    }

                    // 3. Table MFA_APP_POLICIES
                    if (!TableExists(conn, "MFA_APP_POLICIES"))
                    {
                        ExecuteSql(conn, @"
                            CREATE TABLE MFA_APP_POLICIES (
                                APP_ID          VARCHAR2(50)   NOT NULL PRIMARY KEY,
                                POLICY_TYPE     VARCHAR2(30)   DEFAULT 'ALL_USERS',
                                UPDATED_ON      DATE           DEFAULT SYSDATE
                            )");

                        ExecuteSql(conn, "INSERT INTO MFA_APP_POLICIES (APP_ID, POLICY_TYPE) VALUES ('HRMS', 'ALL_USERS')");
                        ExecuteSql(conn, "INSERT INTO MFA_APP_POLICIES (APP_ID, POLICY_TYPE) VALUES ('ERP', 'SPECIFIC_USERS')");
                        ExecuteSql(conn, "INSERT INTO MFA_APP_POLICIES (APP_ID, POLICY_TYPE) VALUES ('PAYROLL', 'ALL_USERS')");
                    }

                    // 4. Table MFA_APP_USER_WHITELIST
                    if (!TableExists(conn, "MFA_APP_USER_WHITELIST"))
                    {
                        ExecuteSql(conn, @"
                            CREATE TABLE MFA_APP_USER_WHITELIST (
                                APP_ID          VARCHAR2(50)   NOT NULL,
                                PCNO            VARCHAR2(50)   NOT NULL,
                                ADDED_ON        DATE           DEFAULT SYSDATE,
                                CONSTRAINT PK_MFA_WHITELIST PRIMARY KEY (APP_ID, PCNO)
                            )");

                        ExecuteSql(conn, "INSERT INTO MFA_APP_USER_WHITELIST (APP_ID, PCNO) VALUES ('ERP', '1001')");
                        ExecuteSql(conn, "INSERT INTO MFA_APP_USER_WHITELIST (APP_ID, PCNO) VALUES ('ERP', '1002')");
                    }

                    // 5. Table MFA_AUDIT_LOGS & Sequence
                    if (!TableExists(conn, "MFA_AUDIT_LOGS"))
                    {
                        ExecuteSql(conn, @"
                            CREATE TABLE MFA_AUDIT_LOGS (
                                LOG_ID          NUMBER         NOT NULL PRIMARY KEY,
                                APP_ID          VARCHAR2(50),
                                PCNO            VARCHAR2(50),
                                ACTION_NAME     VARCHAR2(100),
                                TXN_ID          VARCHAR2(100),
                                MATCH_SCORE     NUMBER(6),
                                STATUS          VARCHAR2(20),
                                CLIENT_IP       VARCHAR2(50),
                                DETAILS         VARCHAR2(255),
                                CREATED_ON      DATE           DEFAULT SYSDATE
                            )");

                        try
                        {
                            ExecuteSql(conn, "CREATE SEQUENCE SEQ_MFA_AUDIT_LOGS START WITH 1 INCREMENT BY 1 NOCACHE");
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("EnsureSchemaExists error: " + ex.Message);
            }
        }

        private static bool TableExists(OracleConnection conn, string tableName)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(1) FROM USER_TABLES WHERE TABLE_NAME = :p_t";
                cmd.Parameters.Add(new OracleParameter("p_t", tableName.ToUpperInvariant()));
                object res = cmd.ExecuteScalar();
                return res != null && Convert.ToInt32(res) > 0;
            }
        }

        private static void ExecuteSql(OracleConnection conn, string sql)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        #region Application & Policy Management

        public static RegisteredApp GetRegisteredApp(string appId)
        {
            if (string.IsNullOrWhiteSpace(appId)) return null;
            try
            {
                EnsureSchemaExists();
                using (var conn = GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        SELECT a.APP_ID, a.APP_NAME, a.BASE_URL, a.CALLBACK_URL, a.SECRET_KEY, a.IS_ACTIVE, a.CREATED_ON,
                               NVL(p.POLICY_TYPE, 'ALL_USERS') AS POLICY_TYPE
                        FROM MFA_REGISTERED_APPS a
                        LEFT JOIN MFA_APP_POLICIES p ON a.APP_ID = p.APP_ID
                        WHERE a.APP_ID = :p_app_id";

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.Parameters.Add(new OracleParameter("p_app_id", appId.Trim()));
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new RegisteredApp
                                {
                                    AppId = reader["APP_ID"].ToString(),
                                    AppName = reader["APP_NAME"].ToString(),
                                    BaseUrl = reader["BASE_URL"] != DBNull.Value ? reader["BASE_URL"].ToString() : "",
                                    CallbackUrl = reader["CALLBACK_URL"].ToString(),
                                    SecretKey = reader["SECRET_KEY"].ToString(),
                                    IsActive = Convert.ToInt32(reader["IS_ACTIVE"]) == 1,
                                    CreatedOn = Convert.ToDateTime(reader["CREATED_ON"]),
                                    PolicyType = reader["POLICY_TYPE"].ToString()
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetRegisteredApp error: " + ex.Message);
            }
            return null;
        }

        public static List<RegisteredApp> GetAllRegisteredApps()
        {
            var list = new List<RegisteredApp>();
            try
            {
                EnsureSchemaExists();
                using (var conn = GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        SELECT a.APP_ID, a.APP_NAME, a.BASE_URL, a.CALLBACK_URL, a.SECRET_KEY, a.IS_ACTIVE, a.CREATED_ON,
                               NVL(p.POLICY_TYPE, 'ALL_USERS') AS POLICY_TYPE
                        FROM MFA_REGISTERED_APPS a
                        LEFT JOIN MFA_APP_POLICIES p ON a.APP_ID = p.APP_ID
                        ORDER BY a.APP_NAME";

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                list.Add(new RegisteredApp
                                {
                                    AppId = reader["APP_ID"].ToString(),
                                    AppName = reader["APP_NAME"].ToString(),
                                    BaseUrl = reader["BASE_URL"] != DBNull.Value ? reader["BASE_URL"].ToString() : "",
                                    CallbackUrl = reader["CALLBACK_URL"].ToString(),
                                    SecretKey = reader["SECRET_KEY"].ToString(),
                                    IsActive = Convert.ToInt32(reader["IS_ACTIVE"]) == 1,
                                    CreatedOn = Convert.ToDateTime(reader["CREATED_ON"]),
                                    PolicyType = reader["POLICY_TYPE"].ToString()
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetAllRegisteredApps error: " + ex.Message);
            }
            return list;
        }

        public static bool SaveRegisteredApp(RegisteredApp app, out string error)
        {
            error = null;
            try
            {
                EnsureSchemaExists();
                using (var conn = GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        MERGE INTO MFA_REGISTERED_APPS t
                        USING (SELECT :p_id AS APP_ID FROM DUAL) s ON (t.APP_ID = s.APP_ID)
                        WHEN MATCHED THEN
                            UPDATE SET t.APP_NAME = :p_name, t.BASE_URL = :p_base, t.CALLBACK_URL = :p_cb, t.SECRET_KEY = :p_key, t.IS_ACTIVE = :p_active
                        WHEN NOT MATCHED THEN
                            INSERT (APP_ID, APP_NAME, BASE_URL, CALLBACK_URL, SECRET_KEY, IS_ACTIVE, CREATED_ON)
                            VALUES (:p_id, :p_name, :p_base, :p_cb, :p_key, :p_active, SYSDATE)";

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.BindByName = true;
                        cmd.Parameters.Add(new OracleParameter("p_id", app.AppId.Trim().ToUpperInvariant()));
                        cmd.Parameters.Add(new OracleParameter("p_name", app.AppName.Trim()));
                        cmd.Parameters.Add(new OracleParameter("p_base", app.BaseUrl ?? ""));
                        cmd.Parameters.Add(new OracleParameter("p_cb", app.CallbackUrl.Trim()));
                        cmd.Parameters.Add(new OracleParameter("p_key", app.SecretKey.Trim()));
                        cmd.Parameters.Add(new OracleParameter("p_active", app.IsActive ? 1 : 0));
                        cmd.ExecuteNonQuery();
                    }

                    // Also save policy
                    string policySql = @"
                        MERGE INTO MFA_APP_POLICIES t
                        USING (SELECT :p_id AS APP_ID FROM DUAL) s ON (t.APP_ID = s.APP_ID)
                        WHEN MATCHED THEN UPDATE SET t.POLICY_TYPE = :p_policy, t.UPDATED_ON = SYSDATE
                        WHEN NOT MATCHED THEN INSERT (APP_ID, POLICY_TYPE, UPDATED_ON) VALUES (:p_id, :p_policy, SYSDATE)";

                    using (var pcmd = conn.CreateCommand())
                    {
                        pcmd.CommandText = policySql;
                        pcmd.BindByName = true;
                        pcmd.Parameters.Add(new OracleParameter("p_id", app.AppId.Trim().ToUpperInvariant()));
                        pcmd.Parameters.Add(new OracleParameter("p_policy", string.IsNullOrEmpty(app.PolicyType) ? "ALL_USERS" : app.PolicyType));
                        pcmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool DeleteRegisteredApp(string appId, out string error)
        {
            error = null;
            try
            {
                EnsureSchemaExists();
                using (var conn = GetConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "DELETE FROM MFA_REGISTERED_APPS WHERE APP_ID = :p_id";
                        cmd.Parameters.Add(new OracleParameter("p_id", appId.Trim().ToUpperInvariant()));
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool IsUserInWhitelist(string appId, string pcno)
        {
            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(pcno)) return false;
            try
            {
                EnsureSchemaExists();
                using (var conn = GetConnection())
                {
                    conn.Open();
                    string sql = "SELECT COUNT(1) FROM MFA_APP_USER_WHITELIST WHERE APP_ID = :p_app AND PCNO = :p_pcno";
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.BindByName = true;
                        cmd.Parameters.Add(new OracleParameter("p_app", appId.Trim().ToUpperInvariant()));
                        cmd.Parameters.Add(new OracleParameter("p_pcno", pcno.Trim()));
                        object res = cmd.ExecuteScalar();
                        return res != null && Convert.ToInt32(res) > 0;
                    }
                }
            }
            catch { return false; }
        }

        public static List<string> GetWhitelistedUsers(string appId)
        {
            var list = new List<string>();
            try
            {
                EnsureSchemaExists();
                using (var conn = GetConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT PCNO FROM MFA_APP_USER_WHITELIST WHERE APP_ID = :p_app ORDER BY PCNO";
                        cmd.Parameters.Add(new OracleParameter("p_app", appId.Trim().ToUpperInvariant()));
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                list.Add(reader["PCNO"].ToString());
                            }
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        public static bool AddUserToWhitelist(string appId, string pcno, out string error)
        {
            error = null;
            try
            {
                EnsureSchemaExists();
                using (var conn = GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        MERGE INTO MFA_APP_USER_WHITELIST t
                        USING (SELECT :p_app AS APP_ID, :p_pcno AS PCNO FROM DUAL) s
                        ON (t.APP_ID = s.APP_ID AND t.PCNO = s.PCNO)
                        WHEN NOT MATCHED THEN INSERT (APP_ID, PCNO, ADDED_ON) VALUES (:p_app, :p_pcno, SYSDATE)";
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.BindByName = true;
                        cmd.Parameters.Add(new OracleParameter("p_app", appId.Trim().ToUpperInvariant()));
                        cmd.Parameters.Add(new OracleParameter("p_pcno", pcno.Trim()));
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public static bool RemoveUserFromWhitelist(string appId, string pcno, out string error)
        {
            error = null;
            try
            {
                EnsureSchemaExists();
                using (var conn = GetConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "DELETE FROM MFA_APP_USER_WHITELIST WHERE APP_ID = :p_app AND PCNO = :p_pcno";
                        cmd.BindByName = true;
                        cmd.Parameters.Add(new OracleParameter("p_app", appId.Trim().ToUpperInvariant()));
                        cmd.Parameters.Add(new OracleParameter("p_pcno", pcno.Trim()));
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        #endregion

        #region Fingerprint Storage (BLOB)

        public static bool SaveFingerprint(string pcno, byte[] templateData, int quality, out string error)
        {
            error = null;
            try
            {
                EnsureSchemaExists();
                using (var conn = GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        MERGE INTO APP_USER_FINGERPRINTS t
                        USING (SELECT :p_pcno AS PCNO FROM DUAL) s ON (t.PCNO = s.PCNO)
                        WHEN MATCHED THEN
                            UPDATE SET t.FINGER_TEMPLATE = :p_template, t.QUALITY = :p_quality, t.UPDATED_ON = SYSDATE
                        WHEN NOT MATCHED THEN
                            INSERT (PCNO, FINGER_TEMPLATE, QUALITY, CREATED_ON, UPDATED_ON)
                            VALUES (:p_pcno, :p_template, :p_quality, SYSDATE, SYSDATE)";

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.BindByName = true;
                        cmd.Parameters.Add(new OracleParameter("p_pcno", OracleDbType.Varchar2, pcno.Trim(), ParameterDirection.Input));
                        var blobParam = new OracleParameter("p_template", OracleDbType.Blob, templateData.Length);
                        blobParam.Value = templateData;
                        cmd.Parameters.Add(blobParam);
                        cmd.Parameters.Add(new OracleParameter("p_quality", OracleDbType.Int32, quality, ParameterDirection.Input));
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public static byte[] GetFingerprint(string pcno, out int quality, out string error)
        {
            quality = 0;
            error = null;
            try
            {
                EnsureSchemaExists();
                using (var conn = GetConnection())
                {
                    conn.Open();
                    string sql = "SELECT FINGER_TEMPLATE, QUALITY FROM APP_USER_FINGERPRINTS WHERE PCNO = :p_pcno";
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.Parameters.Add(new OracleParameter("p_pcno", OracleDbType.Varchar2, pcno.Trim(), ParameterDirection.Input));
                        using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess))
                        {
                            if (reader.Read())
                            {
                                if (!reader.IsDBNull(0))
                                {
                                    long blobLen = reader.GetBytes(0, 0, null, 0, 0);
                                    byte[] buffer = new byte[blobLen];
                                    reader.GetBytes(0, 0, buffer, 0, (int)blobLen);
                                    if (!reader.IsDBNull(1)) quality = Convert.ToInt32(reader.GetValue(1));
                                    return buffer;
                                }
                            }
                        }
                    }
                }
                error = "No enrolled template found for PCNO: " + pcno;
                return null;
            }
            catch (Exception ex) { error = ex.Message; return null; }
        }

        public static bool HasFingerprint(string pcno)
        {
            try
            {
                EnsureSchemaExists();
                using (var conn = GetConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(1) FROM APP_USER_FINGERPRINTS WHERE PCNO = :p_pcno";
                        cmd.Parameters.Add(new OracleParameter("p_pcno", OracleDbType.Varchar2, pcno.Trim(), ParameterDirection.Input));
                        object res = cmd.ExecuteScalar();
                        return res != null && Convert.ToInt32(res) > 0;
                    }
                }
            }
            catch { return false; }
        }

        public static List<EnrolledUser> GetEnrolledUsers()
        {
            var list = new List<EnrolledUser>();
            try
            {
                EnsureSchemaExists();
                using (var conn = GetConnection())
                {
                    conn.Open();
                    string sql = "SELECT PCNO, DBMS_LOB.GETLENGTH(FINGER_TEMPLATE) AS TEMPLATE_LEN, NVL(QUALITY, 0) AS QUALITY, UPDATED_ON FROM APP_USER_FINGERPRINTS ORDER BY UPDATED_ON DESC";
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                list.Add(new EnrolledUser
                                {
                                    PcNo = reader["PCNO"].ToString(),
                                    TemplateLength = reader["TEMPLATE_LEN"] != DBNull.Value ? Convert.ToInt32(reader["TEMPLATE_LEN"]) : 0,
                                    Quality = reader["QUALITY"] != DBNull.Value ? Convert.ToInt32(reader["QUALITY"]) : 0,
                                    UpdatedOn = reader["UPDATED_ON"] != DBNull.Value ? Convert.ToDateTime(reader["UPDATED_ON"]) : DateTime.MinValue
                                });
                            }
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        public static bool DeleteFingerprint(string pcno, out string error)
        {
            error = null;
            try
            {
                EnsureSchemaExists();
                using (var conn = GetConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "DELETE FROM APP_USER_FINGERPRINTS WHERE PCNO = :p_pcno";
                        cmd.Parameters.Add(new OracleParameter("p_pcno", OracleDbType.Varchar2, pcno.Trim(), ParameterDirection.Input));
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        #endregion

        #region Audit Logs & Dashboard Stats

        public static void LogAudit(string appId, string pcno, string action, string txnId, int score, string status, string ip, string details)
        {
            try
            {
                EnsureSchemaExists();
                using (var conn = GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        INSERT INTO MFA_AUDIT_LOGS (LOG_ID, APP_ID, PCNO, ACTION_NAME, TXN_ID, MATCH_SCORE, STATUS, CLIENT_IP, DETAILS, CREATED_ON)
                        VALUES (SEQ_MFA_AUDIT_LOGS.NEXTVAL, :p_app, :p_pcno, :p_act, :p_txn, :p_sc, :p_st, :p_ip, :p_det, SYSDATE)";

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.BindByName = true;
                        cmd.Parameters.Add(new OracleParameter("p_app", appId ?? ""));
                        cmd.Parameters.Add(new OracleParameter("p_pcno", pcno ?? ""));
                        cmd.Parameters.Add(new OracleParameter("p_act", action ?? "LOGIN"));
                        cmd.Parameters.Add(new OracleParameter("p_txn", txnId ?? ""));
                        cmd.Parameters.Add(new OracleParameter("p_sc", score));
                        cmd.Parameters.Add(new OracleParameter("p_st", status ?? ""));
                        cmd.Parameters.Add(new OracleParameter("p_ip", ip ?? ""));
                        cmd.Parameters.Add(new OracleParameter("p_det", details ?? ""));
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LogAudit error: " + ex.Message);
            }
        }

        public static List<AuditLogEntry> GetRecentAuditLogs(int topN = 100)
        {
            var list = new List<AuditLogEntry>();
            try
            {
                EnsureSchemaExists();
                using (var conn = GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        SELECT * FROM (
                            SELECT LOG_ID, APP_ID, PCNO, ACTION_NAME, TXN_ID, MATCH_SCORE, STATUS, CLIENT_IP, DETAILS, CREATED_ON
                            FROM MFA_AUDIT_LOGS
                            ORDER BY CREATED_ON DESC
                        ) WHERE ROWNUM <= :p_top";

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.Parameters.Add(new OracleParameter("p_top", topN));
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                list.Add(new AuditLogEntry
                                {
                                    LogId = Convert.ToInt64(reader["LOG_ID"]),
                                    AppId = reader["APP_ID"].ToString(),
                                    PcNo = reader["PCNO"].ToString(),
                                    ActionName = reader["ACTION_NAME"] != DBNull.Value ? reader["ACTION_NAME"].ToString() : "",
                                    TxnId = reader["TXN_ID"] != DBNull.Value ? reader["TXN_ID"].ToString() : "",
                                    MatchScore = reader["MATCH_SCORE"] != DBNull.Value ? Convert.ToInt32(reader["MATCH_SCORE"]) : 0,
                                    Status = reader["STATUS"].ToString(),
                                    ClientIp = reader["CLIENT_IP"] != DBNull.Value ? reader["CLIENT_IP"].ToString() : "",
                                    Details = reader["DETAILS"] != DBNull.Value ? reader["DETAILS"].ToString() : "",
                                    CreatedOn = Convert.ToDateTime(reader["CREATED_ON"])
                                });
                            }
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        public static DashboardStats GetDashboardStats()
        {
            var stats = new DashboardStats();
            try
            {
                EnsureSchemaExists();
                using (var conn = GetConnection())
                {
                    conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(1), NVL(SUM(CASE WHEN IS_ACTIVE = 1 THEN 1 ELSE 0 END), 0) FROM MFA_REGISTERED_APPS";
                        using (var r = cmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                stats.TotalApps = Convert.ToInt32(r.GetValue(0));
                                stats.ActiveApps = Convert.ToInt32(r.GetValue(1));
                            }
                        }
                    }

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(1) FROM APP_USER_FINGERPRINTS";
                        object res = cmd.ExecuteScalar();
                        if (res != null) stats.TotalEnrolledUsers = Convert.ToInt32(res);
                    }

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT COUNT(1),
                                   NVL(SUM(CASE WHEN STATUS = 'APPROVED' THEN 1 ELSE 0 END), 0),
                                   NVL(SUM(CASE WHEN STATUS = 'DENIED' THEN 1 ELSE 0 END), 0)
                            FROM MFA_AUDIT_LOGS
                            WHERE TRUNC(CREATED_ON) = TRUNC(SYSDATE)";
                        using (var r = cmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                stats.VerificationsToday = Convert.ToInt32(r.GetValue(0));
                                stats.ApprovedToday = Convert.ToInt32(r.GetValue(1));
                                stats.DeniedToday = Convert.ToInt32(r.GetValue(2));
                            }
                        }
                    }
                }
            }
            catch { }
            return stats;
        }

        #endregion
    }
}
