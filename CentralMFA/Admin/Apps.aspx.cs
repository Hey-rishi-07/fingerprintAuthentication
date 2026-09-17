using System;
using System.Web.UI;
using System.Web.UI.WebControls;
using CentralMFA.Utils;

namespace CentralMFA.Admin
{
    public partial class Apps : Page
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

                if (string.IsNullOrEmpty(txtSecretKey.Text))
                {
                    txtSecretKey.Text = "SEC_" + SecurityHelper.GenerateNonce(10).ToUpperInvariant();
                }

                LoadApps();
            }
        }

        protected void btnGenerateKey_Click(object sender, EventArgs e)
        {
            txtSecretKey.Text = "SEC_" + SecurityHelper.GenerateNonce(12).ToUpperInvariant();
        }

        protected void btnSaveApp_Click(object sender, EventArgs e)
        {
            HideStatus();

            string appId = txtAppId.Text.Trim().ToUpperInvariant();
            string appName = txtAppName.Text.Trim();
            string callback = txtCallbackUrl.Text.Trim();
            string secret = txtSecretKey.Text.Trim();

            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(appName) || string.IsNullOrEmpty(callback) || string.IsNullOrEmpty(secret))
            {
                ShowStatus("Please fill in App ID, Application Name, Callback URL, and Secret Key.", "warning");
                return;
            }

            var app = new RegisteredApp
            {
                AppId = appId,
                AppName = appName,
                BaseUrl = txtBaseUrl.Text.Trim(),
                CallbackUrl = callback,
                SecretKey = secret,
                IsActive = chkIsActive.Checked,
                PolicyType = ddlPolicy.SelectedValue
            };

            string error;
            bool saved = DBHelper.SaveRegisteredApp(app, out error);

            if (saved)
            {
                ShowStatus("Application <strong>" + appName + "</strong> saved successfully.", "success");
                ResetForm();
                LoadApps();
            }
            else
            {
                ShowStatus("Save error: " + (error ?? "Unknown database error"), "danger");
            }
        }

        protected void rptApps_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            string targetAppId = e.CommandArgument as string;

            if (e.CommandName == "EditApp")
            {
                var app = DBHelper.GetRegisteredApp(targetAppId);
                if (app != null)
                {
                    txtAppId.Text = app.AppId;
                    txtAppId.Enabled = false; // Primary key locked
                    txtAppName.Text = app.AppName;
                    txtBaseUrl.Text = app.BaseUrl;
                    txtCallbackUrl.Text = app.CallbackUrl;
                    txtSecretKey.Text = app.SecretKey;
                    chkIsActive.Checked = app.IsActive;
                    if (ddlPolicy.Items.FindByValue(app.PolicyType) != null)
                    {
                        ddlPolicy.SelectedValue = app.PolicyType;
                    }

                    litFormTitle.Text = "Edit Application: " + app.AppName;
                    btnCancelEdit.Visible = true;
                    btnSaveApp.Text = "Update Application";
                }
            }
            else if (e.CommandName == "DeleteApp")
            {
                string error;
                bool del = DBHelper.DeleteRegisteredApp(targetAppId, out error);
                if (del)
                {
                    ShowStatus("Application <strong>" + targetAppId + "</strong> deleted.", "info");
                    LoadApps();
                }
                else
                {
                    ShowStatus("Delete failed: " + error, "danger");
                }
            }
        }

        protected void btnCancelEdit_Click(object sender, EventArgs e)
        {
            ResetForm();
        }

        private void ResetForm()
        {
            txtAppId.Text = string.Empty;
            txtAppId.Enabled = true;
            txtAppName.Text = string.Empty;
            txtBaseUrl.Text = string.Empty;
            txtCallbackUrl.Text = string.Empty;
            txtSecretKey.Text = "SEC_" + SecurityHelper.GenerateNonce(10).ToUpperInvariant();
            chkIsActive.Checked = true;
            ddlPolicy.SelectedValue = "ALL_USERS";
            btnCancelEdit.Visible = false;
            litFormTitle.Text = "Register New Application";
            btnSaveApp.Text = "Save Application";
        }

        private void LoadApps()
        {
            var list = DBHelper.GetAllRegisteredApps();
            rptApps.DataSource = list;
            rptApps.DataBind();
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
