using System;
using System.Web.UI;
using CentralMFA.Utils;

namespace CentralMFA.Admin
{
    public partial class Dashboard : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                // Authenticate admin session
                if (Session["IsAdminLoggedIn"] == null || !(bool)Session["IsAdminLoggedIn"])
                {
                    Response.Redirect("Login.aspx");
                    return;
                }

                LoadDashboardData();
            }
        }

        private void LoadDashboardData()
        {
            var stats = DBHelper.GetDashboardStats();
            litTotalApps.Text = stats.TotalApps.ToString();
            litActiveApps.Text = stats.ActiveApps.ToString();
            litEnrolledUsers.Text = stats.TotalEnrolledUsers.ToString();
            litTodayTotal.Text = stats.VerificationsToday.ToString();
            litTodayApproved.Text = stats.ApprovedToday.ToString();
            litTodayDenied.Text = stats.DeniedToday.ToString();

            var apps = DBHelper.GetAllRegisteredApps();
            rptApps.DataSource = apps;
            rptApps.DataBind();

            var logs = DBHelper.GetRecentAuditLogs(10);
            if (logs == null || logs.Count == 0)
            {
                rptAudit.Visible = false;
                pnlNoAudit.Visible = true;
            }
            else
            {
                rptAudit.Visible = true;
                pnlNoAudit.Visible = false;
                rptAudit.DataSource = logs;
                rptAudit.DataBind();
            }
        }

        protected void btnLogout_Click(object sender, EventArgs e)
        {
            Session.Clear();
            Response.Redirect("Login.aspx");
        }
    }
}
