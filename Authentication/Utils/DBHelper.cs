using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace Authentication.Utils
{
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
                  ?? ConfigurationManager.ConnectionStrings["AttendanceDB"]
                  ?? ConfigurationManager.ConnectionStrings["CompanyDB"];
            
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

        /// <summary>
        /// Tests if Oracle 11g database connection is reachable.
        /// </summary>
        public static bool TestConnection(out string message)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT 1 FROM DUAL";
                        cmd.ExecuteScalar();
                    }
                }
                message = "Connected to Oracle database successfully.";
                return true;
            }
            catch (Exception ex)
            {
                message = "Oracle connection failed: " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Automatically checks and creates the APP_USER_FINGERPRINTS table in Oracle if it doesn't exist yet.
        /// </summary>
        public static void EnsureFingerprintTableExists()
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    // Check if table exists
                    bool tableExists = false;
                    using (var checkCmd = conn.CreateCommand())
                    {
                        checkCmd.CommandText = "SELECT COUNT(1) FROM USER_TABLES WHERE TABLE_NAME = 'APP_USER_FINGERPRINTS'";
                        object res = checkCmd.ExecuteScalar();
                        if (res != null && Convert.ToInt32(res) > 0)
                        {
                            tableExists = true;
                        }
                    }

                    if (!tableExists)
                    {
                        using (var createCmd = conn.CreateCommand())
                        {
                            createCmd.CommandText = @"
                                CREATE TABLE APP_USER_FINGERPRINTS (
                                    PCNO            VARCHAR2(50) NOT NULL PRIMARY KEY,
                                    FINGER_TEMPLATE BLOB NOT NULL,
                                    QUALITY         NUMBER(3) DEFAULT 0,
                                    CREATED_ON      DATE DEFAULT SYSDATE,
                                    UPDATED_ON      DATE DEFAULT SYSDATE
                                )";
                            createCmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("EnsureFingerprintTableExists error: " + ex.Message);
            }
        }

        /// <summary>
        /// Saves or updates the fingerprint template for a specific PCNO in Oracle 11g.
        /// </summary>
        public static bool SaveFingerprint(string pcno, byte[] templateData, int quality, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(pcno))
            {
                error = "PCNO cannot be empty.";
                return false;
            }
            if (templateData == null || templateData.Length == 0)
            {
                error = "Fingerprint template data is empty.";
                return false;
            }

            try
            {
                EnsureFingerprintTableExists();

                using (var conn = GetConnection())
                {
                    conn.Open();

                    // Using Oracle MERGE for UPSERT
                    string sql = @"
                        MERGE INTO APP_USER_FINGERPRINTS t
                        USING (SELECT :p_pcno AS PCNO FROM DUAL) s
                        ON (t.PCNO = s.PCNO)
                        WHEN MATCHED THEN
                            UPDATE SET t.FINGER_TEMPLATE = :p_template,
                                       t.QUALITY         = :p_quality,
                                       t.UPDATED_ON      = SYSDATE
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
            catch (Exception ex)
            {
                error = "Oracle Save Error: " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Retrieves the enrolled fingerprint template BLOB for a specific PCNO from Oracle 11g.
        /// </summary>
        public static byte[] GetFingerprint(string pcno, out int quality, out string error)
        {
            quality = 0;
            error = null;

            if (string.IsNullOrWhiteSpace(pcno))
            {
                error = "PCNO cannot be empty.";
                return null;
            }

            try
            {
                EnsureFingerprintTableExists();

                using (var conn = GetConnection())
                {
                    conn.Open();

                    string sql = "SELECT FINGER_TEMPLATE, QUALITY FROM APP_USER_FINGERPRINTS WHERE PCNO = :p_pcno";
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.BindByName = true;
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

                                    if (!reader.IsDBNull(1))
                                    {
                                        quality = Convert.ToInt32(reader.GetValue(1));
                                    }
                                    return buffer;
                                }
                            }
                        }
                    }
                }

                error = "No enrolled fingerprint found in Oracle for PCNO: " + pcno;
                return null;
            }
            catch (Exception ex)
            {
                error = "Oracle Query Error: " + ex.Message;
                return null;
            }
        }

        /// <summary>
        /// Checks if an enrolled fingerprint exists for the PCNO.
        /// </summary>
        public static bool HasFingerprint(string pcno)
        {
            if (string.IsNullOrWhiteSpace(pcno)) return false;

            try
            {
                EnsureFingerprintTableExists();

                using (var conn = GetConnection())
                {
                    conn.Open();
                    string sql = "SELECT COUNT(1) FROM APP_USER_FINGERPRINTS WHERE PCNO = :p_pcno";
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.BindByName = true;
                        cmd.Parameters.Add(new OracleParameter("p_pcno", OracleDbType.Varchar2, pcno.Trim(), ParameterDirection.Input));
                        object res = cmd.ExecuteScalar();
                        return res != null && Convert.ToInt32(res) > 0;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Deletes the fingerprint record for testing.
        /// </summary>
        public static bool DeleteFingerprint(string pcno, out string error)
        {
            error = null;
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();
                    string sql = "DELETE FROM APP_USER_FINGERPRINTS WHERE PCNO = :p_pcno";
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.BindByName = true;
                        cmd.Parameters.Add(new OracleParameter("p_pcno", OracleDbType.Varchar2, pcno.Trim(), ParameterDirection.Input));
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = "Oracle Delete Error: " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Gets all enrolled users from Oracle database.
        /// </summary>
        public static List<EnrolledUser> GetEnrolledUsers()
        {
            var list = new List<EnrolledUser>();
            try
            {
                EnsureFingerprintTableExists();

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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetEnrolledUsers error: " + ex.Message);
            }
            return list;
        }
    }
}
