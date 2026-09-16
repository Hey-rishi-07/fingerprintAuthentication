using System;
using System.Web.UI;
using System.Web.UI.WebControls;
using Authentication.Utils;

namespace Authentication
{
    public partial class Enroll : Page
    {
        private bool IsDemoMode
        {
            get
            {
                if (Session["DemoMode"] != null)
                {
                    return (bool)Session["DemoMode"];
                }
                return chkDemoMode.Checked;
            }
            set
            {
                Session["DemoMode"] = value;
                chkDemoMode.Checked = value;
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                if (Session["DemoMode"] == null)
                {
                    int testInit = DeviceManager.Instance.InitDevice();
                    IsDemoMode = (testInit != 0);
                }
                else
                {
                    chkDemoMode.Checked = IsDemoMode;
                }

                // If PCNO passed in query string from Login page, pre-fill it
                string queryPcNo = Request.QueryString["pcno"];
                if (!string.IsNullOrEmpty(queryPcNo))
                {
                    txtPcNo.Text = queryPcNo.Trim();
                }

                UpdateDeviceStatusUI();
                LoadEnrolledUsers();
            }
        }

        protected void chkDemoMode_CheckedChanged(object sender, EventArgs e)
        {
            IsDemoMode = chkDemoMode.Checked;
            UpdateDeviceStatusUI();
            if (IsDemoMode)
            {
                ShowStatus("Demo Mode enabled (simulated MFS100 captures).", "info");
            }
            else
            {
                HideStatus();
            }
        }

        private void UpdateDeviceStatusUI()
        {
            if (IsDemoMode)
            {
                lblDeviceStatus.InnerText = "Mode: Demo Mode (Simulating MFS100 without hardware)";
            }
            else
            {
                var dev = DeviceManager.Instance;
                if (dev.IsInitialized)
                {
                    lblDeviceStatus.InnerText = "Scanner: Connected & Ready (MFS100)";
                }
                else
                {
                    lblDeviceStatus.InnerText = "Scanner: Connecting... (Check USB cable)";
                }
            }
        }

        protected void btnCapture_Click(object sender, EventArgs e)
        {
            HideStatus();

            string pcno = txtPcNo.Text.Trim();
            if (string.IsNullOrEmpty(pcno))
            {
                ShowStatus("Please enter an Employee ID / PCNO first.", "warning");
                return;
            }

            int quality;
            string captureError;
            byte[] template = DeviceManager.Instance.CaptureFingerprint(IsDemoMode, pcno, out quality, out captureError);

            if (template == null)
            {
                ShowStatus("Fingerprint capture failed: " + (captureError ?? "Unknown error"), "danger");
                pnlCaptureMeta.Visible = false;
                btnSaveToDb.Enabled = false;
                return;
            }

            // Save captured template into session
            Session["Enroll_Template"] = template;
            Session["Enroll_Quality"] = quality;
            Session["Enroll_PCNO"] = pcno;

            // Update UI
            pnlCaptureMeta.Visible = true;
            litTemplateSize.Text = template.Length.ToString();
            litQuality.Text = quality.ToString();
            barQuality.Style["width"] = quality + "%";
            btnSaveToDb.Enabled = true;

            ShowStatus("Fingerprint captured successfully! Now click <strong>'2. Save to Oracle Database'</strong>.", "success");
        }

        protected void btnSaveToDb_Click(object sender, EventArgs e)
        {
            HideStatus();

            byte[] template = Session["Enroll_Template"] as byte[];
            int quality = Session["Enroll_Quality"] != null ? Convert.ToInt32(Session["Enroll_Quality"]) : 0;
            string pcno = txtPcNo.Text.Trim();

            if (template == null || template.Length == 0 || string.IsNullOrEmpty(pcno))
            {
                ShowStatus("No captured fingerprint template found in session. Please click '1. Capture Fingerprint' first.", "warning");
                return;
            }

            string dbError;
            bool saved = DBHelper.SaveFingerprint(pcno, template, quality, out dbError);

            if (saved)
            {
                ShowStatus("Success! Fingerprint for PCNO: <strong>" + pcno + "</strong> has been saved directly to Oracle 11g database as BLOB.", "success");
                btnSaveToDb.Enabled = false;
                Session["Enroll_Template"] = null;
                LoadEnrolledUsers();
            }
            else
            {
                ShowStatus("Failed to save to Oracle 11g: " + (dbError ?? "Unknown database error"), "danger");
            }
        }

        protected void btnTestVerify_Click(object sender, EventArgs e)
        {
            HideStatus();

            string pcno = txtPcNo.Text.Trim();
            if (string.IsNullOrEmpty(pcno))
            {
                ShowStatus("Please enter the PCNO to test verify.", "warning");
                return;
            }

            // Fetch template from Oracle DB
            int dbQuality;
            string dbError;
            byte[] storedTemplate = DBHelper.GetFingerprint(pcno, out dbQuality, out dbError);

            if (storedTemplate == null)
            {
                ShowStatus("Cannot test: No template found in Oracle 11g for PCNO: " + pcno + ". Please capture and save first.", "warning");
                return;
            }

            // Capture new probe template
            int probeQuality;
            string captureError;
            byte[] probeTemplate = DeviceManager.Instance.CaptureFingerprint(IsDemoMode, pcno, out probeQuality, out captureError);

            if (probeTemplate == null)
            {
                ShowStatus("Test scan failed: " + captureError, "danger");
                return;
            }

            // Match
            string matchError;
            int score = DeviceManager.Instance.MatchFingerprints(storedTemplate, probeTemplate, IsDemoMode, out matchError);

            if (score >= DeviceManager.MatchThreshold)
            {
                ShowStatus("Verification Match Succeeded! Match score: <strong>" + score + "</strong> (Threshold: " + DeviceManager.MatchThreshold + "). This fingerprint will successfully authenticate this PCNO.", "success");
            }
            else
            {
                ShowStatus("Verification Failed! Score: " + score + " (Threshold: " + DeviceManager.MatchThreshold + "). Fingerprint does not match the stored template for PCNO: " + pcno, "danger");
            }
        }

        protected void btnRefreshList_Click(object sender, EventArgs e)
        {
            LoadEnrolledUsers();
            ShowStatus("Enrolled user list refreshed from Oracle database.", "info");
        }

        protected void rptEnrolledUsers_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            if (e.CommandName == "DeleteUser")
            {
                string targetPcNo = e.CommandArgument as string;
                if (!string.IsNullOrEmpty(targetPcNo))
                {
                    string delError;
                    bool deleted = DBHelper.DeleteFingerprint(targetPcNo, out delError);
                    if (deleted)
                    {
                        ShowStatus("Fingerprint record for PCNO: " + targetPcNo + " deleted from Oracle database.", "info");
                        LoadEnrolledUsers();
                    }
                    else
                    {
                        ShowStatus("Delete failed: " + delError, "danger");
                    }
                }
            }
        }

        private void LoadEnrolledUsers()
        {
            var list = DBHelper.GetEnrolledUsers();
            if (list == null || list.Count == 0)
            {
                rptEnrolledUsers.Visible = false;
                pnlNoUsers.Visible = true;
            }
            else
            {
                rptEnrolledUsers.Visible = true;
                pnlNoUsers.Visible = false;
                rptEnrolledUsers.DataSource = list;
                rptEnrolledUsers.DataBind();
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
