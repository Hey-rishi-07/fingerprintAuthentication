using System;
using System.Configuration;
using System.DirectoryServices;

namespace Authentication.Utils
{
    public static class ADHelper
    {
        /// <summary>
        /// Authenticates the user against Active Directory LDAP path and returns the associated EmployeeID / PCNO.
        /// Includes local mock bypass accounts for testing without domain connectivity.
        /// </summary>
        public static string AuthenticateAndGetPCNO(string username, string password, out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(username))
            {
                errorMessage = "Username cannot be empty.";
                return null;
            }

            // Local offline testing bypass accounts (ideal when testing without Active Directory connection)
            string uLower = username.Trim().ToLower();
            if (uLower == "1001" || uLower == "admin" || uLower == "aadmin")
            {
                return "1001";
            }
            if (uLower == "1002")
            {
                return "1002";
            }
            if (uLower == "1003")
            {
                return "1003";
            }
            if (uLower == "1004")
            {
                return "1004";
            }

            // If no password provided for non-bypass accounts
            if (string.IsNullOrEmpty(password))
            {
                errorMessage = "Password cannot be empty.";
                return null;
            }

            string ldapPath = ConfigurationManager.AppSettings["ADConnectionPath"];
            if (string.IsNullOrWhiteSpace(ldapPath))
            {
                errorMessage = "ADConnectionPath is not configured in Web.config.";
                return null;
            }

            try
            {
                // In .NET DirectoryServices, DirectoryEntry does not validate credentials until NativeObject or search is performed
                using (DirectoryEntry entry = new DirectoryEntry(ldapPath, username, password))
                {
                    object native = entry.NativeObject; // Forces authentication

                    using (DirectorySearcher search = new DirectorySearcher(entry))
                    {
                        search.Filter = "(SAMAccountName=" + username.Trim() + ")";
                        search.PropertiesToLoad.Add("EmployeeID");
                        search.PropertiesToLoad.Add("displayName");
                        search.PropertiesToLoad.Add("mail");

                        SearchResult result = search.FindOne();
                        if (result != null)
                        {
                            using (DirectoryEntry dsResult = result.GetDirectoryEntry())
                            {
                                if (dsResult.Properties.Contains("EmployeeID") && dsResult.Properties["EmployeeID"].Count > 0)
                                {
                                    return dsResult.Properties["EmployeeID"][0].ToString();
                                }

                                // Fallback for local AD setups where EmployeeID might not be populated
                                return username.Trim();
                            }
                        }
                        else
                        {
                            errorMessage = "Authentication succeeded, but the user record was not found in Active Directory.";
                            return null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Active Directory error: " + ex.Message;
                return null;
            }
        }
    }
}
