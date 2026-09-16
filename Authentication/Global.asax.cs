using System;
using System.Web;
using Authentication.Utils;

namespace Authentication
{
    public class Global : HttpApplication
    {
        protected void Application_Start(object sender, EventArgs e)
        {
            try
            {
                // Verify or auto-create Oracle 11g table on application startup
                DBHelper.EnsureFingerprintTableExists();
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
