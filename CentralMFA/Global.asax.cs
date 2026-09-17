using System;
using System.Web;
using CentralMFA.Utils;

namespace CentralMFA
{
    public class Global : HttpApplication
    {
        protected void Application_Start(object sender, EventArgs e)
        {
            try
            {
                // Ensure Oracle 11g tables, sequences, and seed apps exist
                DBHelper.EnsureSchemaExists();
            }
            catch { }
        }

        protected void Application_End(object sender, EventArgs e)
        {
            try
            {
                DeviceManager.Instance.UninitDevice();
            }
            catch { }
        }
    }
}
