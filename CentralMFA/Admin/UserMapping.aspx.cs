using System;
using System.Web.UI;
using System.Web.UI.WebControls;
using CentralMFA.Utils;

namespace CentralMFA.Admin
{
    public partial class UserMapping : Page
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

                string qApp = Request.QueryString["appId"];
                if (!string.IsNullOrEmpty(qApp) && ddlApps.Items.FindByValue(qApp) != null)
                {
                    ddlApps.SelectedValue = qApp;
                }

                LoadMappedUsers();
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
            LoadMappedUsers();
        }

        private void LoadMappedUsers()
        {
            if (ddlApps.SelectedItem == null) return;
            string appId = ddlApps.SelectedValue;

            litAppName.Text = ddlApps.SelectedItem.Text;

            var users = DBHelper.GetWhitelistedUsers(appId);
            if (users == null || users.Count == 0)
            {
                rptWhitelistedUsers.Visible = false;
                pnlNoUsers.Visible = true;
            }
            else
            {
                rptWhitelistedUsers.Visible = true;
                pnlNoUsers.Visible = false;
                rptWhitelistedUsers.DataSource = users;
                rptWhitelistedUsers.DataBind();
            }
        }

        protected void btnAddUser_Click(object sender, EventArgs e)
        {
            HideStatus();

            string appId = ddlApps.SelectedValue;
            string pcno = txtAddPcno.Text.Trim();

            if (string.IsNullOrEmpty(pcno))
            {
                ShowStatus("Please enter an Employee ID / PCNO to add.", "warning");
                return;
            }

            string err;
            bool added = DBHelper.AddUserToWhitelist(appId, pcno, out err);
            if (added)
            {
                ShowStatus("PCNO: <strong>" + pcno + "</strong> added to mandatory MFA list for " + appId, "success");
                txtAddPcno.Text = string.Empty;
                LoadMappedUsers();
            }
            else
            {
                ShowStatus("Failed to add user: " + err, "danger");
            }
        }

        protected void rptWhitelistedUsers_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            if (e.CommandName == "RemoveUser")
            {
                string appId = ddlApps.SelectedValue;
                string targetPcno = e.CommandArgument as string;

                if (!string.IsNullOrEmpty(targetPcno))
                {
                    string err;
                    bool removed = DBHelper.RemoveUserFromWhitelist(appId, targetPcno, out err);
                    if (removed)
                    {
                        ShowStatus("PCNO: <strong>" + targetPcno + "</strong> removed from " + appId, "info");
                        LoadMappedUsers();
                    }
                    else
                    {
                        ShowStatus("Remove failed: " + err, "danger");
                    }
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
