using System;
using System.Web.UI;
using Authentication.Utils;

namespace Authentication
{
    public partial class Login : Page
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
                // If demo mode is in session or hardware is not present, default demo mode gracefully
                if (Session["DemoMode"] == null)
                {
                    // Check if scanner initializes, else default to Demo Mode for seamless developer experience
                    int testInit = DeviceManager.Instance.InitDevice();
                    IsDemoMode = (testInit != 0);
                }
                else
                {
                    chkDemoMode.Checked = IsDemoMode;
                }

                UpdateDeviceStatusUI();
            }
        }

        protected void chkDemoMode_CheckedChanged(object sender, EventArgs e)
        {
            IsDemoMode = chkDemoMode.Checked;
            UpdateDeviceStatusUI();
            if (IsDemoMode)
            {
                ShowStatus("Running in DEMO MODE (simulated fingerprint hardware).", "info");
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
                lblDeviceStatus.InnerText = "Mode: Demo Simulation (No physical MFS100 device needed)";
            }
            else
            {
                var dev = DeviceManager.Instance;
                if (dev.IsInitialized)
                {
                    lblDeviceStatus.InnerText = "Scanner: Connected & Ready (Mantra MFS100)";
                }
                else
                {
                    lblDeviceStatus.InnerText = "Scanner: Connecting... (Plug in MFS100 USB)";
                }
            }
        }

        protected void btnVerifyAD_Click(object sender, EventArgs e)
        {
            HideStatus();

            string username = txtUsername.Text.Trim();
            string password = txtPassword.Text;

            string errorMsg;
            string pcno = ADHelper.AuthenticateAndGetPCNO(username, password, out errorMsg);

            if (string.IsNullOrEmpty(pcno))
            {
                ShowStatus(string.IsNullOrEmpty(errorMsg) ? "Invalid Active Directory credentials." : errorMsg, "danger");
                return;
            }

            // Step 1 Passed: Store pending PCNO in session
            Session["PendingPCNO"] = pcno;
            litAuthenticatedPcNo.Text = pcno;

            // Transition Stepper to Step 2
            pnlStep1.Visible = false;
            pnlStep2.Visible = true;
            stepIndicator1.Attributes["class"] = "step-item completed";
            stepIndicator2.Attributes["class"] = "step-item active";

            // Check if user has an enrolled fingerprint in Oracle database
            bool hasEnrolled = DBHelper.HasFingerprint(pcno);
            if (!hasEnrolled)
            {
                ShowStatus("Notice: No enrolled fingerprint found in Oracle 11g for PCNO: <strong>" + pcno + "</strong>. " +
                           "Please <a href='Enroll.aspx?pcno=" + Server.UrlEncode(pcno) + "' style='text-decoration:underline; font-weight:700;'>Enroll your fingerprint first</a>, or verify using Demo Mode.", "warning");
            }
            else
            {
                ShowStatus("Active Directory verified! Please proceed with Step 2 (Fingerprint scan).", "success");
            }

            UpdateDeviceStatusUI();
        }

        protected void btnScanAndVerify_Click(object sender, EventArgs e)
        {
            HideStatus();

            string pcno = Session["PendingPCNO"] as string;
            if (string.IsNullOrEmpty(pcno))
            {
                ShowStatus("Session expired. Please restart the login process.", "danger");
                ResetToStep1();
                return;
            }

            // 1. Retrieve enrolled fingerprint template from Oracle 11g
            int enrolledQuality;
            string dbError;
            byte[] storedTemplate = DBHelper.GetFingerprint(pcno, out enrolledQuality, out dbError);

            if (storedTemplate == null || storedTemplate.Length == 0)
            {
                ShowStatus("Verification Error: No template stored in Oracle for PCNO: <strong>" + pcno + "</strong>.<br/>" +
                           (string.IsNullOrEmpty(dbError) ? "" : "DB Detail: " + dbError + "<br/>") +
                           "<a href='Enroll.aspx?pcno=" + Server.UrlEncode(pcno) + "' class='btn btn-secondary btn-sm' style='margin-top:8px;'>Go to Fingerprint Enrollment</a>", "danger");
                return;
            }

            // 2. Capture live fingerprint from Mantra MFS100 scanner
            int capturedQuality;
            string captureError;
            byte[] liveTemplate = DeviceManager.Instance.CaptureFingerprint(IsDemoMode, pcno, out capturedQuality, out captureError);

            if (liveTemplate == null)
            {
                ShowStatus("Fingerprint capture failed: " + (captureError ?? "Unknown capture error"), "danger");
                return;
            }

            // 3. Match captured template against stored template from Oracle
            string matchError;
            int matchScore = DeviceManager.Instance.MatchFingerprints(storedTemplate, liveTemplate, IsDemoMode, out matchError);

            if (!string.IsNullOrEmpty(matchError))
            {
                ShowStatus("Matching algorithm error: " + matchError, "danger");
                return;
            }

            // 4. Evaluate match score against threshold (1400)
            if (matchScore >= DeviceManager.MatchThreshold)
            {
                // MFA SUCCEEDED! Complete login session
                Session["AuthenticatedPCNO"] = pcno;
                Session["MfaScore"] = matchScore;
                Session["MfaQuality"] = capturedQuality;
                Session["MfaTime"] = DateTime.Now;
                Session["MfaMode"] = IsDemoMode ? "Demo Mode (Simulated)" : "Mantra MFS100 Live Hardware";

                Response.Redirect("Success.aspx");
            }
            else
            {
                ShowStatus("Fingerprint verification failed! Match score: <strong>" + matchScore + "</strong> (Required: " + DeviceManager.MatchThreshold + "). Fingerprint does not match the enrolled template for PCNO: " + pcno, "danger");
            }
        }

        protected void btnBackToStep1_Click(object sender, EventArgs e)
        {
            ResetToStep1();
        }

        private void ResetToStep1()
        {
            Session["PendingPCNO"] = null;
            pnlStep1.Visible = true;
            pnlStep2.Visible = false;
            stepIndicator1.Attributes["class"] = "step-item active";
            stepIndicator2.Attributes["class"] = "step-item";
            HideStatus();
        }

        private void ShowStatus(string message, string type = "danger")
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
