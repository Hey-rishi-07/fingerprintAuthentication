using System;
using System.Web.UI;
using System.Web.UI.WebControls;
using CentralMFA.Utils;

namespace CentralMFA
{
    public partial class Enroll : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                string qPcno = Request.QueryString["pcno"];
                if (!string.IsNullOrEmpty(qPcno))
                {
                    txtPcNo.Text = qPcno.Trim();
                }
                LoadEnrolledUsers();
            }
        }

        protected void btnSaveToDb_Click(object sender, EventArgs e)
        {
            HideStatus();

            string pcno = txtPcNo.Text.Trim();
            string templateBase64 = hfEnrollTemplate.Value;
            int quality = 0;
            int.TryParse(hfEnrollQuality.Value, out quality);

            if (string.IsNullOrEmpty(pcno))
            {
                ShowStatus("Please enter an Employee ID / PCNO.", "warning");
                return;
            }

            if (string.IsNullOrEmpty(templateBase64))
            {
                ShowStatus("No captured fingerprint template found. Please capture first.", "warning");
                return;
            }

            byte[] templateBytes;
            try
            {
                templateBytes = Convert.FromBase64String(templateBase64);
            }
            catch
            {
                ShowStatus("Invalid fingerprint template data format.", "danger");
                return;
            }

            string dbError;
            bool saved = DBHelper.SaveFingerprint(pcno, templateBytes, quality, out dbError);

            if (saved)
            {
                ShowStatus("Success! Fingerprint for PCNO: <strong>" + pcno + "</strong> has been saved directly to Oracle 11g database as BLOB.", "success");
                btnSaveToDb.Enabled = false;
                hfEnrollTemplate.Value = string.Empty;
                LoadEnrolledUsers();
            }
            else
            {
                ShowStatus("Failed to save to Oracle 11g: " + (dbError ?? "Unknown error"), "danger");
            }
        }

        protected void btnRefresh_Click(object sender, EventArgs e)
        {
            LoadEnrolledUsers();
            ShowStatus("User list refreshed from Oracle database.", "info");
        }

        protected void rptUsers_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            if (e.CommandName == "DeleteUser")
            {
                string targetPcno = e.CommandArgument as string;
                if (!string.IsNullOrEmpty(targetPcno))
                {
                    string delErr;
                    bool deleted = DBHelper.DeleteFingerprint(targetPcno, out delErr);
                    if (deleted)
                    {
                        ShowStatus("Fingerprint for PCNO: " + targetPcno + " deleted from Oracle.", "info");
                        LoadEnrolledUsers();
                    }
                    else
                    {
                        ShowStatus("Delete failed: " + delErr, "danger");
                    }
                }
            }
        }

        private void LoadEnrolledUsers()
        {
            var list = DBHelper.GetEnrolledUsers();
            if (list == null || list.Count == 0)
            {
                rptUsers.Visible = false;
                pnlNoUsers.Visible = true;
            }
            else
            {
                rptUsers.Visible = true;
                pnlNoUsers.Visible = false;
                rptUsers.DataSource = list;
                rptUsers.DataBind();
            }
        }

        private void ShowStatus(string message, string type = "info")
        {
            pnlStatus.Visible = true;
            statusBox.Attributes["class"] = "alert alert-" + type;
            litStatusMessage.Text = message;
        }

        private void HideStatus()
        {
            pnlStatus.Visible = false;
            litStatusMessage.Text = string.Empty;
        }
    }
}
