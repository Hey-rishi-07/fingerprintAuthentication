using System;
using System.Web;
using System.Web.UI;
using CentralMFA.Utils;

namespace CentralMFA.Samples
{
    public partial class SampleCallingApp_CSharp : Page
    {
        // Secret key registered in CentralMFA Admin for AppId "ERP"
        private const string APP_ID = "ERP";
        private const string APP_SECRET = "ERP_SECRET_KEY_77B1C9D2";

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                // Check if user is being redirected back from CentralMFA Gateway
                string status = Request.QueryString["status"];
                if (!string.IsNullOrEmpty(status))
                {
                    HandleMfaCallback(status);
                }
            }
        }

        private void HandleMfaCallback(string status)
        {
            string appId = Request.QueryString["appId"] ?? "";
            string pcno = Request.QueryString["pcno"] ?? "";
            string action = Request.QueryString["action"] ?? "LOGIN";
            string txnId = Request.QueryString["txnId"] ?? "";
            string timestamp = Request.QueryString["timestamp"] ?? "";
            string nonce = Request.QueryString["nonce"] ?? "";
            string signature = Request.QueryString["sig"] ?? Request.QueryString["signature"] ?? "";

            // 1. Verify HMAC-SHA256 signature to guarantee response authenticity and prevent URL tampering
            string dataToVerify = SecurityHelper.BuildPayload(appId, pcno, status, action, txnId, timestamp, nonce);

            bool isValidSignature = SecurityHelper.VerifyHmacSha256(dataToVerify, signature, APP_SECRET);

            pnlNotice.Visible = true;
            if (!isValidSignature)
            {
                noticeBox.Attributes["class"] = "alert alert-danger";
                litNotice.Text = "<strong>Security Error:</strong> Cryptographic HMAC signature verification failed! The response from the MFA Gateway was tampered with or corrupted.";
                return;
            }

            // 2. Verify timestamp freshness (within 2 minutes)
            if (!SecurityHelper.IsTimestampValid(timestamp, 120))
            {
                noticeBox.Attributes["class"] = "alert alert-warning";
                litNotice.Text = "<strong>Expired Token:</strong> The MFA response has expired (older than 120 seconds). Please retry.";
                return;
            }

            // 3. Check verification status
            if (status.Equals("APPROVED", StringComparison.OrdinalIgnoreCase))
            {
                noticeBox.Attributes["class"] = "alert alert-success";
                if (action.Equals("LOGIN", StringComparison.OrdinalIgnoreCase))
                {
                    litNotice.Text = string.Format("<strong>MFA Login Approved!</strong> Biometric verification succeeded for Employee PCNO: <strong>{0}</strong>. Proceeding into ERP portal dashboard...", Server.HtmlEncode(pcno));
                }
                else
                {
                    litNotice.Text = string.Format("<strong>Transaction Authorized!</strong> Action: <strong>{0}</strong> | TxnRef: <strong>{1}</strong> | Approved by PCNO: <strong>{2}</strong>.", Server.HtmlEncode(action), Server.HtmlEncode(txnId), Server.HtmlEncode(pcno));
                }
            }
            else
            {
                noticeBox.Attributes["class"] = "alert alert-danger";
                litNotice.Text = string.Format("<strong>Biometric MFA Denied:</strong> Status: {0} for PCNO: {1}. Access or transaction rejected.", Server.HtmlEncode(status), Server.HtmlEncode(pcno));
            }
        }

        protected void btnLoginMFA_Click(object sender, EventArgs e)
        {
            string pcno = txtLdapPcno.Text.Trim();
            if (string.IsNullOrEmpty(pcno))
            {
                pnlNotice.Visible = true;
                noticeBox.Attributes["class"] = "alert alert-warning";
                litNotice.Text = "Please enter an Employee PCNO.";
                return;
            }

            // Generate returnUrl back to this sample page
            string returnUrl = Request.Url.GetLeftPart(UriPartial.Path);
            RedirectToMfaGateway(pcno, "LOGIN", "", returnUrl);
        }

        protected void btnAuthorizeTxn_Click(object sender, EventArgs e)
        {
            string pcno = txtApproverPcno.Text.Trim();
            string action = txtActionType.Text.Trim();
            string txnId = txtTxnId.Text.Trim();

            if (string.IsNullOrEmpty(pcno) || string.IsNullOrEmpty(action))
            {
                pnlNotice.Visible = true;
                noticeBox.Attributes["class"] = "alert alert-warning";
                litNotice.Text = "Please enter Approver PCNO and Action Identifier.";
                return;
            }

            string returnUrl = Request.Url.GetLeftPart(UriPartial.Path);
            RedirectToMfaGateway(pcno, action, txnId, returnUrl);
        }

        private void RedirectToMfaGateway(string pcno, string action, string txnId, string returnUrl)
        {
            string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            string nonce = SecurityHelper.GenerateNonce(16);

            // Data string signed with HMAC-SHA256
            string dataToSign = string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}",
                APP_ID, pcno, returnUrl, action, txnId, timestamp, nonce);

            string signature = SecurityHelper.ComputeHmacSha256(dataToSign, APP_SECRET);

            // Build Gateway URL
            string gatewayUrl = string.Format("../Gateway.aspx?appId={0}&pcno={1}&returnUrl={2}&action={3}&txnId={4}&timestamp={5}&nonce={6}&signature={7}",
                HttpUtility.UrlEncode(APP_ID),
                HttpUtility.UrlEncode(pcno),
                HttpUtility.UrlEncode(returnUrl),
                HttpUtility.UrlEncode(action),
                HttpUtility.UrlEncode(txnId),
                HttpUtility.UrlEncode(timestamp),
                HttpUtility.UrlEncode(nonce),
                HttpUtility.UrlEncode(signature));

            Response.Redirect(gatewayUrl);
        }
    }
}
