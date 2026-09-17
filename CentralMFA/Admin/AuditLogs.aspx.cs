using System;
using System.Web.UI;
using CentralMFA.Utils;

namespace CentralMFA.Admin
{
    public partial class AuditLogs : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                if (Session["IsAdminLoggedIn"] == null || !(bool)Session["IsAdminLoggedIn"])
                {
                    Response.Redirect("Login.aspx");
                    return;
                }

                LoadLogs();
            }
        }

        protected void btnRefresh_Click(object sender, EventArgs e)
        {
            LoadLogs();
        }

        private void LoadLogs()
        {
            var list = DBHelper.GetRecentAuditLogs(100);
            if (list == null || list.Count == 0)
            {
                rptLogs.Visible = false;
                pnlNoLogs.Visible = true;
            }
            else
            {
                rptLogs.Visible = true;
                pnlNoLogs.Visible = false;
                rptLogs.DataSource = list;
                rptLogs.DataBind();
            }
        }

        protected void btnLogout_Click(object sender, EventArgs e)
        {
            Session.Clear();
            Response.Redirect("Login.aspx");
        }
    }
}
