using System;
using System.Web.UI;
using CentralMFA.Utils;

namespace CentralMFA
{
    public partial class Gateway : Page
    {
        protected string AppId
        {
            get { return (Request.QueryString["appId"] ?? "HRMS").Trim().ToUpperInvariant(); }
        }

        protected string PcNo
        {
            get { return (Request.QueryString["pcno"] ?? "1001").Trim(); }
        }

        protected string ActionName
        {
            get { return (Request.QueryString["action"] ?? "LOGIN").Trim(); }
        }

        protected string TxnId
        {
            get { return (Request.QueryString["txnId"] ?? "").Trim(); }
        }

        protected string ReturnUrl
        {
            get { return Request.QueryString["returnUrl"]; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                EvaluateAppAndPolicy();
            }
        }

        private void EvaluateAppAndPolicy()
        {
            RegisteredApp app = DBHelper.GetRegisteredApp(AppId);
            if (app == null)
            {
                ShowStatus("Invalid Application: Application ID '" + AppId + "' is not registered in the MFA Gateway.", "danger");
                return;
            }

            if (!app.IsActive)
            {
                ShowStatus("MFA Service Disabled: Application '" + app.AppName + "' has been temporarily deactivated by an administrator.", "warning");
                return;
            }

            // Set UI details
            litAppTitle.Text = app.AppName;
            litPcNo.Text = PcNo;

            if (!string.Equals(ActionName, "LOGIN", StringComparison.OrdinalIgnoreCase))
            {
                pnlActionBanner.Visible = true;
                litActionName.Text = ActionName;
                litTxnId.Text = string.IsNullOrEmpty(TxnId) ? "N/A" : TxnId;
                litSubtitle.Text = "Please authenticate your fingerprint to authorize this action.";
            }
            else
            {
                pnlActionBanner.Visible = false;
                litSubtitle.Text = "Biometric Login Verification";
            }

            // Evaluate Policy Rules
            string policy = app.PolicyType ?? "ALL_USERS";

            if (string.Equals(policy, "DISABLED", StringComparison.OrdinalIgnoreCase))
            {
                // Policy says MFA is disabled for this app: auto-bypass
                CompleteAndRedirect(app, "APPROVED", 0, "MFA_POLICY_DISABLED");
                return;
            }

            if (string.Equals(policy, "SPECIFIC_USERS", StringComparison.OrdinalIgnoreCase))
            {
                bool isRequired = DBHelper.IsUserInWhitelist(AppId, PcNo);
                if (!isRequired)
                {
                    // User is not in the mandatory MFA list: auto-bypass
                    CompleteAndRedirect(app, "APPROVED", 0, "EXEMPT_USER_POLICY");
                    return;
                }
            }

            if (string.Equals(policy, "ENROLLED_ONLY", StringComparison.OrdinalIgnoreCase))
            {
                bool hasEnrolled = DBHelper.HasFingerprint(PcNo);
                if (!hasEnrolled)
                {
                    // User has no enrolled fingerprint: bypass
                    CompleteAndRedirect(app, "APPROVED", 0, "NO_ENROLLED_FINGERPRINT");
                    return;
                }
            }

            // Check if user has an enrolled fingerprint template in Oracle
            if (!DBHelper.HasFingerprint(PcNo))
            {
                ShowStatus("Notice: No registered fingerprint template found in Oracle 11g for PCNO: <strong>" + PcNo + "</strong>. " +
                           "Please <a href='Enroll.aspx?pcno=" + Server.UrlEncode(PcNo) + "' style='text-decoration:underline; font-weight:700;'>Enroll your fingerprint first</a>, or click 'Test with Simulated Scan (Demo)'.", "warning");
            }
        }

        protected void btnServerVerify_Click(object sender, EventArgs e)
        {
            HideStatus();

            RegisteredApp app = DBHelper.GetRegisteredApp(AppId);
            if (app == null || !app.IsActive)
            {
                ShowStatus("Application authentication failed: App is invalid or inactive.", "danger");
                return;
            }

            bool isDemo = (hfIsDemo.Value == "1");
            string capturedBase64 = hfCapturedTemplate.Value;

            byte[] liveTemplate = null;
            if (isDemo)
            {
                liveTemplate = DeviceManager.Instance.GenerateDemoTemplate(PcNo);
            }
            else
            {
                if (string.IsNullOrEmpty(capturedBase64))
                {
                    ShowStatus("No fingerprint data was received from the scanner.", "danger");
                    return;
                }
                try
                {
                    liveTemplate = Convert.FromBase64String(capturedBase64);
                }
                catch
                {
                    ShowStatus("Invalid fingerprint template data received from client.", "danger");
                    return;
                }
            }

            // Fetch stored template from Oracle 11g
            int storedQuality;
            string dbError;
            byte[] storedTemplate = DBHelper.GetFingerprint(PcNo, out storedQuality, out dbError);

            if (storedTemplate == null || storedTemplate.Length == 0)
            {
                if (isDemo)
                {
                    // Generate matching demo template for seamless testing
                    storedTemplate = liveTemplate;
                }
                else
                {
                    ShowStatus("No enrolled fingerprint found in Oracle 11g for PCNO: <strong>" + PcNo + "</strong>. " +
                               "<a href='Enroll.aspx?pcno=" + Server.UrlEncode(PcNo) + "' class='btn btn-secondary btn-sm' style='margin-top:6px;'>Enroll Fingerprint</a>", "danger");
                    return;
                }
            }

            // Match templates
            string matchError;
            int matchScore = DeviceManager.Instance.MatchFingerprints(storedTemplate, liveTemplate, isDemo, out matchError);

            if (!string.IsNullOrEmpty(matchError))
            {
                ShowStatus("Matching algorithm error: " + matchError, "danger");
                return;
            }

            if (matchScore >= DeviceManager.MatchThreshold)
            {
                // Biometric Match Succeeded!
                CompleteAndRedirect(app, "APPROVED", matchScore, "BIOMETRIC_MATCH_SUCCESS");
            }
            else
            {
                // Mismatch
                string clientIp = Request.UserHostAddress;
                DBHelper.LogAudit(AppId, PcNo, ActionName, TxnId, matchScore, "DENIED", clientIp, "Match Score: " + matchScore + " (Below threshold " + DeviceManager.MatchThreshold + ")");

                ShowStatus("Fingerprint Verification Failed! Match score: <strong>" + matchScore + "</strong> (Required: " + DeviceManager.MatchThreshold + "). Fingerprint does not match the enrolled template for PCNO: " + PcNo, "danger");
            }
        }

        private void CompleteAndRedirect(RegisteredApp app, string status, int matchScore, string details)
        {
            string clientIp = Request.UserHostAddress;
            DBHelper.LogAudit(AppId, PcNo, ActionName, TxnId, matchScore, status, clientIp, details);

            // Determine target callback URL
            string targetUrl = ReturnUrl;
            if (string.IsNullOrWhiteSpace(targetUrl))
            {
                targetUrl = app.CallbackUrl;
            }

            // Build HMAC signature
            long timestamp = SecurityHelper.GetCurrentEpoch();
            string nonce = SecurityHelper.GenerateNonce(12);
            string payload = SecurityHelper.BuildPayload(AppId, PcNo, status, ActionName, TxnId, timestamp, nonce);
            string sig = SecurityHelper.ComputeHmacSha256(payload, app.SecretKey);

            // Construct redirect URL
            string separator = targetUrl.Contains("?") ? "&" : "?";
            string redirectUrl = string.Format("{0}{1}status={2}&appId={3}&pcno={4}&action={5}&txnId={6}&score={7}&timestamp={8}&nonce={9}&sig={10}",
                targetUrl,
                separator,
                Server.UrlEncode(status),
                Server.UrlEncode(AppId),
                Server.UrlEncode(PcNo),
                Server.UrlEncode(ActionName),
                Server.UrlEncode(TxnId),
                matchScore,
                timestamp,
                Server.UrlEncode(nonce),
                Server.UrlEncode(sig));

            Response.Redirect(redirectUrl, false);
            Context.ApplicationInstance.CompleteRequest();
        }

        protected void btnCancel_Click(object sender, EventArgs e)
        {
            RegisteredApp app = DBHelper.GetRegisteredApp(AppId);
            if (app != null)
            {
                CompleteAndRedirect(app, "CANCELLED", 0, "USER_CANCELLED");
            }
            else
            {
                Response.Redirect("Gateway.aspx");
            }
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
