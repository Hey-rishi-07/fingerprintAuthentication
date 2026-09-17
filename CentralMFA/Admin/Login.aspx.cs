using System;
using System.Configuration;
using System.Web.Security;
using System.Web.UI;

namespace CentralMFA.Admin
{
    public partial class Login : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                if (Session["IsAdminLoggedIn"] != null && (bool)Session["IsAdminLoggedIn"])
                {
                    Response.Redirect("Dashboard.aspx");
                }
            }
        }

        protected void btnLogin_Click(object sender, EventArgs e)
        {
            string u = txtUsername.Text.Trim();
            string p = txtPassword.Text;

            string cfgUser = ConfigurationManager.AppSettings["AdminUsername"] ?? "admin";
            string cfgPass = ConfigurationManager.AppSettings["AdminPassword"] ?? "admin123";

            if (string.Equals(u, cfgUser, StringComparison.OrdinalIgnoreCase) && string.Equals(p, cfgPass))
            {
                Session["IsAdminLoggedIn"] = true;
                Session["AdminUser"] = u;
                FormsAuthentication.SetAuthCookie(u, false);
                Response.Redirect("Dashboard.aspx");
            }
            else
            {
                pnlStatus.Visible = true;
                litStatusMessage.Text = "Invalid administrator credentials. Please check Web.config.";
            }
        }
    }
}
