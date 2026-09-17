using System;
using System.Web.UI;
using System.Web.UI.WebControls;
using CentralMFA.Utils;

namespace CentralMFA.Admin
{
    public partial class Policies : Page
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

                LoadAppsDropdown();

                string qAppId = Request.QueryString["appId"];
                if (!string.IsNullOrEmpty(qAppId) && ddlApps.Items.FindByValue(qAppId) != null)
                {
                    ddlApps.SelectedValue = qAppId;
                }

                LoadCurrentAppPolicy();
            }
        }

        private void LoadAppsDropdown()
        {
            ddlApps.Items.Clear();
            var list = DBHelper.GetAllRegisteredApps();
            foreach (var app in list)
            {
                ddlApps.Items.Add(new ListItem(app.AppName + " (" + app.AppId + ")", app.AppId));
            }
        }

        protected void ddlApps_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadCurrentAppPolicy();
        }

        private void LoadCurrentAppPolicy()
        {
            if (ddlApps.SelectedItem == null) return;
            string selectedAppId = ddlApps.SelectedValue;

            var app = DBHelper.GetRegisteredApp(selectedAppId);
            if (app != null)
            {
                string policy = string.IsNullOrEmpty(app.PolicyType) ? "ALL_USERS" : app.PolicyType;
                if (rblPolicy.Items.FindByValue(policy) != null)
                {
                    rblPolicy.SelectedValue = policy;
                }
                lnkUserMapping.NavigateUrl = "UserMapping.aspx?appId=" + Server.UrlEncode(selectedAppId);
            }
        }

        protected void btnSavePolicy_Click(object sender, EventArgs e)
        {
            HideStatus();

            if (ddlApps.SelectedItem == null)
            {
                ShowStatus("Please select an application.", "warning");
                return;
            }

            string appId = ddlApps.SelectedValue;
            string policy = rblPolicy.SelectedValue;

            var app = DBHelper.GetRegisteredApp(appId);
            if (app != null)
            {
                app.PolicyType = policy;
                string err;
                bool saved = DBHelper.SaveRegisteredApp(app, out err);
                if (saved)
                {
                    ShowStatus("Policy for <strong>" + app.AppName + "</strong> updated to: <strong>" + policy + "</strong>.", "success");
                }
                else
                {
                    ShowStatus("Policy update failed: " + err, "danger");
                }
            }
        }

        protected void btnLogout_Click(object sender, EventArgs e)
        {
            Session.Clear();
            Response.Redirect("Login.aspx");
        }

        private void ShowStatus(string msg, string type = "info")
        {
            pnlStatus.Visible = true;
            statusBox.Attributes["class"] = "alert alert-" + type;
            litStatusMessage.Text = msg;
        }

        private void HideStatus()
        {
            pnlStatus.Visible = false;
            litStatusMessage.Text = string.Empty;
        }
    }
}
