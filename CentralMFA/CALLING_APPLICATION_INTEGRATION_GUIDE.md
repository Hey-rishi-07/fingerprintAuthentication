# CentralMFA - Calling Application Integration Guide

This guide provides complete, step-by-step instructions for integrating any intranet application (**ASP.NET C#, PHP, Classic ASP, Java, Python**, etc.) with **CentralMFA**.

It covers both core use cases:
1. **Post-LDAP Login MFA**: Enforcing biometric fingerprint verification immediately after an employee enters valid Active Directory / LDAP credentials.
2. **Submit-Button / Step-Up Transaction Authorization**: Requiring biometric sign-off before executing sensitive actions (e.g., approving payroll, releasing a purchase order, or modifying critical records).

---

## Table of Contents

1. [Architecture & How It Works](#1-architecture--how-it-works)
2. [Step 0: Register Your Application in CentralMFA Admin](#2-step-0-register-your-application-in-centralmfa-admin)
3. [Exact Specification of Data Sent TO CentralMFA](#3-exact-specification-of-data-sent-to-centralmfa)
4. [Exact Specification of Data Returned FROM CentralMFA](#4-exact-specification-of-data-returned-from-centralmfa)
5. [Cryptographic Signature & Verification Formula](#5-cryptographic-signature--verification-formula)
6. [Complete Code Examples by Language](#6-complete-code-examples-by-language)
   - [A. ASP.NET (C# - Web Forms & MVC)](#a-aspnet-c---web-forms--mvc)
   - [B. PHP (5.4 / 7.x / 8.x)](#b-php-54--7x--8x)
   - [C. Classic ASP (VBScript)](#c-classic-asp-vbscript)
   - [D. Java (Spring Boot / Servlets)](#d-java-spring-boot--servlets)
7. [Step-by-Step Testing & Verification](#7-step-by-step-testing--verification)
8. [Troubleshooting & Common Pitfalls](#8-troubleshooting--common-pitfalls)

---

## 1. Architecture & How It Works

**No SDK or DLL installation is required in your calling application!**  
All communication occurs via standard HTTP/HTTPS browser redirects secured with keyed **HMAC-SHA256** signatures.

```
┌───────────────────────────┐                       ┌───────────────────────────────┐
│   Calling Application     │                       │     CentralMFA Server         │
│  (HRMS / ERP / Payroll)   │                       │  (Gateway.aspx on Intranet)   │
└─────────────┬─────────────┘                       └───────────────┬───────────────┘
              │                                                     │
 1. LDAP Pass │                                                     │
    or Submit │                                                     │
    Button    │ ── 2. HTTP Redirect with AppId, PCNO, Action ─────► │
              │                                                     │
              │                                                     │ 3. Check Policy in Oracle
              │                                                     │ 4. Read MFS100 (Port 8034)
              │                                                     │ 5. Match Fingerprint
              │                                                     │ 6. Generate HMAC Signature
              │                                                     │
              │ ◄── 7. Signed HTTP Callback (status=APPROVED, sig) ─┤
              │                                                     │
 8. Verify    │                                                     │
    Signature │                                                     │
    & Proceed │                                                     │
```

---

## 2. Step 0: Register Your Application in CentralMFA Admin

Before your application can communicate with CentralMFA, an administrator must register it:

1. Open `http://<mfa-server>/CentralMFA/Admin/Login.aspx` (Default: `admin` / `admin123`).
2. Navigate to **Applications** (`Admin/Apps.aspx`).
3. Fill in the form:
   - **AppId**: A unique uppercase identifier (e.g. `ERP`, `HRMS`, `PAYROLL`).
   - **App Name**: The display title shown to users (e.g. *Enterprise ERP Portal*).
   - **Base URL**: The root URL of your app (e.g. `http://erp.company.local/`).
   - **Callback URL**: The default landing endpoint after verification (e.g. `http://erp.company.local/MfaCallback.aspx`).
   - **Secret Key**: Click **"Generate 256-bit Key"** (or specify your own). **Copy this key** — your calling application will use it to verify return signatures.
   - **Enforcement Policy**: Choose `ALL_USERS`, `SPECIFIC_USERS`, `ENROLLED_ONLY`, or `DISABLED`.
4. Click **Save Application**.

---

## 3. Exact Specification of Data Sent TO CentralMFA

To initiate biometric verification, your application constructs an HTTP redirect URL to `Gateway.aspx`.

* **Endpoint**: `http://<central-mfa-host>/CentralMFA/Gateway.aspx`
* **Method**: HTTP `GET` (Browser Redirect)

### Query String Parameters

| Parameter | Type | Required? | Description | Example |
|---|---|---|---|---|
| `appId` | String | **Yes** | The unique registered application identifier. | `ERP` |
| `pcno` | String | **Yes** | Employee PCNO or username retrieved from LDAP. | `EMP001` |
| `returnUrl` | String | **Yes** | URL-encoded address where CentralMFA should redirect the user upon completion. | `http%3A%2F%2Ferp.local%2FCallback.aspx` |
| `action` | String | No | Purpose of request. Default is `LOGIN`. For submit buttons, use custom action. | `LOGIN` or `APPROVE_PAYROLL` |
| `txnId` | String | No | Unique transaction reference ID (used for submit buttons). | `TXN-2026-9912` |
| `timestamp` | String/Long | Optional | Unix epoch in seconds or `yyyyMMddHHmmss` UTC. | `1789650031` |
| `nonce` | String | Optional | Random 12–16 character string to prevent replay. | `a9f3b18c4e02` |
| `sig` | String | Optional | HMAC-SHA256 signature of outgoing request parameters. | `5e1223fbb4...` |

### URL Examples

#### A. Post-LDAP Login MFA URL:
```text
http://intranet-mfa/CentralMFA/Gateway.aspx?appId=ERP&pcno=EMP001&returnUrl=http%3A%2F%2Ferp.corp%2FMfaCallback.aspx&action=LOGIN
```

#### B. Sensitive Submit Button / Step-Up Authorization URL:
```text
http://intranet-mfa/CentralMFA/Gateway.aspx?appId=ERP&pcno=EMP001&returnUrl=http%3A%2F%2Ferp.corp%2FAuthorizeCallback.aspx&action=APPROVE_PAYROLL&txnId=TXN-2026-9912
```

---

## 4. Exact Specification of Data Returned FROM CentralMFA

After CentralMFA completes biometric verification (or policy evaluation), it redirects the employee's browser back to your `returnUrl` with the following parameters:

* **Endpoint**: Your application's `returnUrl` (e.g. `http://erp.corp/MfaCallback.aspx`)
* **Method**: HTTP `GET` redirect

### Return Parameters

| Parameter | Type | Always Present? | Description | Example Values |
|---|---|---|---|---|
| `status` | String | **Yes** | Final outcome of the verification. | `APPROVED`, `DENIED`, `CANCELLED` |
| `appId` | String | **Yes** | The application identifier that made the request. | `ERP` |
| `pcno` | String | **Yes** | The employee PCNO that was verified. | `EMP001` |
| `action` | String | **Yes** | The action requested (`LOGIN`, `APPROVE_PAYROLL`, etc.). | `LOGIN` |
| `txnId` | String | **Yes** | Transaction reference ID (empty if `LOGIN`). | `TXN-2026-9912` |
| `score` | Integer | **Yes** | Biometric matching score (Threshold: 1400). `0` if policy-bypassed. | `3850` |
| `timestamp` | Long | **Yes** | UTC Unix epoch timestamp when token was created. | `1789650031` |
| `nonce` | String | **Yes** | 12-character random hex nonce. | `2f3dba27046e` |
| `sig` | String | **Yes** | 64-character lowercase hexadecimal HMAC-SHA256 signature. | `5e1223fbb4887826f12065c6...` |

### Example Return URL:
```text
http://erp.corp/MfaCallback.aspx?status=APPROVED&appId=ERP&pcno=EMP001&action=LOGIN&txnId=&score=3850&timestamp=1789650031&nonce=2f3dba27046e&sig=5e1223fbb4887826f12065c6cc72094aa30828dce965457f3304930524a5c543
```

---

## 5. Cryptographic Signature & Verification Formula

To guarantee that users cannot alter `status=DENIED` to `status=APPROVED` in their browser, CentralMFA signs the response using your application's registered secret key.

### Canonical Message Payload:
The exact pipe-delimited string format is:
```text
payload = appId + "|" + pcno + "|" + status + "|" + action + "|" + txnId + "|" + timestamp + "|" + nonce
```

### Signature Computation:
```text
expectedSignature = HMAC-SHA256(payload, secretKey)   // 64-character hex string
```

### 3-Step Verification Checklist in Calling App:
1. **Signature Match**: Compute `expectedSignature` and compare with incoming `sig` using a constant-time / timing-safe string comparison.
2. **Timestamp Freshness**: Verify `abs(currentEpoch - timestamp) <= 120` (reject responses older than 2 minutes to prevent replay attacks).
3. **Status Check**: Verify `status.ToUpper() == "APPROVED"`.

---

## 6. Complete Code Examples by Language

### A. ASP.NET (C# - Web Forms & MVC)

#### Step 1: Reusable Security Helper (`CentralMfaHelper.cs`)
```csharp
using System;
using System.Security.Cryptography;
using System.Text;

public static class CentralMfaHelper
{
    public static string ComputeHmacSha256(string message, string secretKey)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(secretKey);
        byte[] messageBytes = Encoding.UTF8.GetBytes(message);
        using (var hmac = new HMACSHA256(keyBytes))
        {
            byte[] hash = hmac.ComputeHash(messageBytes);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }
    }

    public static bool VerifyHmacSha256(string message, string signature, string secretKey)
    {
        if (string.IsNullOrEmpty(message) || string.IsNullOrEmpty(signature)) return false;
        string expected = ComputeHmacSha256(message, secretKey);
        if (expected.Length != signature.Length) return false;
        
        int diff = 0;
        for (int i = 0; i < expected.Length; i++) diff |= expected[i] ^ signature[i];
        return diff == 0;
    }

    public static bool IsTimestampFresh(long timestamp, int maxAgeSeconds = 120)
    {
        long now = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
        return Math.Abs(now - timestamp) <= maxAgeSeconds;
    }

    public static string BuildPayload(string appId, string pcno, string status, string action, string txnId, string timestamp, string nonce)
    {
        return string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}",
            appId ?? "", pcno ?? "", status ?? "", action ?? "", txnId ?? "", timestamp ?? "", nonce ?? "");
    }
}
```

#### Step 2: Triggering MFA after LDAP Login (`Login.aspx.cs`)
```csharp
// Once employee passes standard LDAP authentication:
string pcno = txtUsername.Text.Trim(); // e.g. "EMP001"
string mfaGateway = "http://intranet-mfa/CentralMFA/Gateway.aspx";
string appId = "ERP";
string returnUrl = "http://erp.corp/MfaCallback.aspx";

string redirectUrl = string.Format("{0}?appId={1}&pcno={2}&returnUrl={3}&action=LOGIN",
    mfaGateway,
    Server.UrlEncode(appId),
    Server.UrlEncode(pcno),
    Server.UrlEncode(returnUrl));

Response.Redirect(redirectUrl);
```

#### Step 3: Triggering MFA on a Submit Button (`Payroll.aspx.cs`)
```csharp
protected void btnApprovePayroll_Click(object sender, EventArgs e)
{
    string pcno = Session["UserPCNO"].ToString(); // "EMP001"
    string txnId = "PAY-2026-0881";
    string mfaGateway = "http://intranet-mfa/CentralMFA/Gateway.aspx";
    string appId = "ERP";
    string returnUrl = "http://erp.corp/PayrollCallback.aspx";

    string redirectUrl = string.Format("{0}?appId={1}&pcno={2}&returnUrl={3}&action=APPROVE_PAYROLL&txnId={4}",
        mfaGateway,
        Server.UrlEncode(appId),
        Server.UrlEncode(pcno),
        Server.UrlEncode(returnUrl),
        Server.UrlEncode(txnId));

    Response.Redirect(redirectUrl);
}
```

#### Step 4: Validating Callback in Returning Page (`MfaCallback.aspx.cs`)
```csharp
protected void Page_Load(object sender, EventArgs e)
{
    string secretKey = "ERP_SECRET_KEY_77B1C9D2"; // From CentralMFA Admin

    string status    = Request.QueryString["status"] ?? "";
    string appId     = Request.QueryString["appId"] ?? "";
    string pcno      = Request.QueryString["pcno"] ?? "";
    string action    = Request.QueryString["action"] ?? "LOGIN";
    string txnId     = Request.QueryString["txnId"] ?? "";
    string timestamp = Request.QueryString["timestamp"] ?? "";
    string nonce     = Request.QueryString["nonce"] ?? "";
    string sig       = Request.QueryString["sig"] ?? Request.QueryString["signature"] ?? "";

    // 1. Verify HMAC-SHA256 Signature
    string payload = CentralMfaHelper.BuildPayload(appId, pcno, status, action, txnId, timestamp, nonce);
    bool isValidSig = CentralMfaHelper.VerifyHmacSha256(payload, sig, secretKey);

    if (!isValidSig)
    {
        // Potential URL tampering!
        Response.Write("Security Error: Invalid cryptographic signature.");
        Response.End();
        return;
    }

    // 2. Check Timestamp Freshness
    long epoch;
    if (long.TryParse(timestamp, out epoch) && !CentralMfaHelper.IsTimestampFresh(epoch, 120))
    {
        Response.Write("Security Error: Token has expired (> 120 seconds). Please retry.");
        Response.End();
        return;
    }

    // 3. Check Approval Status
    if (status.Equals("APPROVED", StringComparison.OrdinalIgnoreCase))
    {
        // Biometric MFA successful!
        FormsAuthentication.SetAuthCookie(pcno, false);
        Session["AuthenticatedUser"] = pcno;
        Response.Redirect("Default.aspx");
    }
    else
    {
        Response.Write("Biometric MFA Denied. Access rejected.");
    }
}
```

---

### B. PHP (5.4 / 7.x / 8.x)

#### Step 1: Initiating MFA after LDAP Login (`login.php`)
```php
<?php
// When LDAP credentials pass:
$pcno = $_POST['username']; // e.g. "EMP001"
$mfaGateway = "http://intranet-mfa/CentralMFA/Gateway.aspx";
$appId = "HRMS";
$returnUrl = "http://hrms.corp/callback.php";

$params = http_build_query([
    'appId'     => $appId,
    'pcno'      => $pcno,
    'returnUrl' => $returnUrl,
    'action'    => 'LOGIN'
]);

header("Location: {$mfaGateway}?{$params}");
exit;
?>
```

#### Step 2: Initiating MFA on Sensitive Submit Button (`leave_approve.php`)
```php
<?php
// Sensitive transaction submit button:
$pcno  = $_SESSION['pcno']; // "EMP001"
$txnId = "LV-2026-9041";
$mfaGateway = "http://intranet-mfa/CentralMFA/Gateway.aspx";

$params = http_build_query([
    'appId'     => 'HRMS',
    'pcno'      => $pcno,
    'returnUrl' => 'http://hrms.corp/leave_callback.php',
    'action'    => 'SANCTION_LEAVE',
    'txnId'     => $txnId
]);

header("Location: {$mfaGateway}?{$params}");
exit;
?>
```

#### Step 3: Validating Callback in Returning Script (`callback.php`)
```php
<?php
$secretKey = 'HRMS_SECRET_KEY_8F9A2B1C'; // From CentralMFA Admin

$status    = $_GET['status'] ?? '';
$appId     = $_GET['appId'] ?? '';
$pcno      = $_GET['pcno'] ?? '';
$action    = $_GET['action'] ?? 'LOGIN';
$txnId     = $_GET['txnId'] ?? '';
$timestamp = $_GET['timestamp'] ?? '';
$nonce     = $_GET['nonce'] ?? '';
$sig       = $_GET['sig'] ?? $_GET['signature'] ?? '';

// 1. Verify HMAC-SHA256 Signature
$payload = "{$appId}|{$pcno}|{$status}|{$action}|{$txnId}|{$timestamp}|{$nonce}";
$expectedSig = hash_hmac('sha256', $payload, $secretKey);

if (!hash_equals(strtolower($expectedSig), strtolower($sig))) {
    die("Security Error: Invalid cryptographic signature. Request tampered with.");
}

// 2. Verify Timestamp Freshness (120 seconds)
if (is_numeric($timestamp) && abs(time() - (int)$timestamp) > 120) {
    die("Security Error: MFA response token expired. Please retry.");
}

// 3. Check Approval
if (strcasecmp($status, 'APPROVED') === 0) {
    // Biometric verified!
    session_start();
    $_SESSION['auth_user'] = $pcno;
    header("Location: dashboard.php");
    exit;
} else {
    die("Biometric MFA Denied for user " . htmlspecialchars($pcno));
}
?>
```

---

### C. Classic ASP (VBScript)

#### Step 1: Initiating MFA (`login.asp`)
```asp
<%@ Language="VBScript" %>
<%
Dim pcno, mfaGateway, appId, returnUrl, redirectUrl
pcno        = Trim(Request.Form("username"))
mfaGateway  = "http://intranet-mfa/CentralMFA/Gateway.aspx"
appId       = "ERP"
returnUrl   = "http://" & Request.ServerVariables("HTTP_HOST") & "/mfa_callback.asp"

redirectUrl = mfaGateway & "?appId=" & Server.URLEncode(appId) & _
              "&pcno=" & Server.URLEncode(pcno) & _
              "&returnUrl=" & Server.URLEncode(returnUrl) & _
              "&action=LOGIN"

Response.Redirect redirectUrl
%>
```

#### Step 2: Validating Callback (`mfa_callback.asp`)
```asp
<%@ Language="VBScript" %>
<%
' --- Function: Compute HMAC-SHA256 using built-in Windows .NET COM ---
Function ComputeHmacSha256(dataStr, secretStr)
    On Error Resume Next
    Dim utf8, hmac, dataBytes, keyBytes, hashBytes, i, hexStr
    Set utf8 = CreateObject("System.Text.UTF8Encoding")
    Set hmac = CreateObject("System.Security.Cryptography.HMACSHA256")
    
    keyBytes = utf8.GetBytes_4(secretStr)
    hmac.Key = keyBytes
    dataBytes = utf8.GetBytes_4(dataStr)
    hashBytes = hmac.ComputeHash_2(dataBytes)
    
    hexStr = ""
    For i = 1 To LenB(hashBytes)
        hexStr = hexStr & Right("0" & Hex(AscB(MidB(hashBytes, i, 1))), 2)
    Next
    ComputeHmacSha256 = LCase(hexStr)
    On Error GoTo 0
End Function

Const SECRET_KEY = "ERP_SECRET_KEY_77B1C9D2"

Dim status, retAppId, pcno, action, txnId, ts, nonce, sig, payload, expectedSig
status   = Request.QueryString("status")
retAppId = Request.QueryString("appId")
pcno     = Request.QueryString("pcno")
action   = Request.QueryString("action")
txnId    = Request.QueryString("txnId")
ts       = Request.QueryString("timestamp")
nonce    = Request.QueryString("nonce")
sig      = Request.QueryString("sig")
If sig = "" Then sig = Request.QueryString("signature")

' 1. Verify HMAC-SHA256
payload = retAppId & "|" & pcno & "|" & status & "|" & action & "|" & txnId & "|" & ts & "|" & nonce
expectedSig = ComputeHmacSha256(payload, SECRET_KEY)

If LCase(sig) <> LCase(expectedSig) Then
    Response.Write "Security Error: Signature mismatch. Tampering detected."
    Response.End
ElseIf UCase(status) = "APPROVED" Then
    ' Biometric MFA Approved!
    Session("LoggedInUser") = pcno
    Response.Redirect "main_menu.asp"
Else
    Response.Write "Biometric MFA Denied for user " & Server.HTMLEncode(pcno)
End If
%>
```

---

### D. Java (Spring Boot / Servlets)

#### Helper Utility (`CentralMfaClient.java`):
```java
import javax.crypto.Mac;
import javax.crypto.spec.SecretKeySpec;
import java.nio.charset.StandardCharsets;

public class CentralMfaClient {
    public static String computeHmacSha256(String message, String secretKey) throws Exception {
        Mac hmac = Mac.getInstance("HmacSHA256");
        SecretKeySpec keySpec = new SecretKeySpec(secretKey.getBytes(StandardCharsets.UTF_8), "HmacSHA256");
        hmac.init(keySpec);
        byte[] hash = hmac.doFinal(message.getBytes(StandardCharsets.UTF_8));
        
        StringBuilder hexString = new StringBuilder();
        for (byte b : hash) {
            String hex = Integer.toHexString(0xff & b);
            if (hex.length() == 1) hexString.append('0');
            hexString.append(hex);
        }
        return hexString.toString();
    }

    public static boolean verifySignature(String payload, String signature, String secretKey) {
        try {
            String expected = computeHmacSha256(payload, secretKey);
            return expected.equalsIgnoreCase(signature);
        } catch (Exception e) {
            return false;
        }
    }
}
```

---

## 7. Step-by-Step Testing & Verification

1. **Verify Your Secret Key**:
   Ensure the `SECRET_KEY` in your calling application matches the key in `Admin/Apps.aspx`.
2. **Test with the Built-In C# Demo**:
   Open `http://localhost:51050/Samples/SampleCallingApp_CSharp.aspx` in your browser.
   - Test Scenario 1 (Post-LDAP Login MFA): Click **"Simulate LDAP Login"** -> View the Gateway -> Fingerprint scan -> Redirect back with `MFA Login Approved!`.
   - Test Scenario 2 (Submit Button): Click **"Authorize Transaction"** -> View the Gateway showing `APPROVE_PAYROLL` -> Scan -> Redirect back with `Transaction Authorized!`.
3. **Simulated vs Physical Scanner**:
   - If physical scanner is connected: Keep finger on the sensor. The auto-scan on load will capture automatically!
   - If testing without hardware: Click **"Simulate Scan (Demo)"** to immediately test the return signature and callback logic.

---

## 8. Troubleshooting & Common Pitfalls

| Issue | Cause | Solution |
|---|---|---|
| **Signature Mismatch Error** | The calling app and CentralMFA have different secret keys. | Copy the secret key from `Admin/Apps.aspx` into your app's config. |
| **Field Order Discrepancy** | Payload string fields constructed in different order. | Always use: `appId\|pcno\|status\|action\|txnId\|timestamp\|nonce`. |
| **Expired Token Error** | Server time on calling app and MFA server differ by > 120s. | Synchronize system clocks across intranet servers using NTP / Active Directory Domain Controller. |
| **Invalid Application Error** | `appId` passed in query string does not match `APP_ID` in Oracle. | Verify that the `appId` in the URL matches the record in `Admin/Apps.aspx`. |
| **Scanner Port 8034 Offline** | Employee PC is not running `MfaScannerAgent.exe`. | Run `install.bat` once on the employee PC, or launch `MfaScannerAgent.exe`. |
| **Auto-Redirect Loop** | Callback page redirects back to Gateway instead of continuing. | In your callback handler, set the session/cookie and redirect to your dashboard, not back to the login page. |
