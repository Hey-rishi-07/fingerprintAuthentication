<%@ Language="VBScript" %>
<%
' ==============================================================================
' CentralMFA Calling Application Integration Sample - Classic ASP (VBScript)
' ==============================================================================
' Demonstrates:
' 1. Initiating Biometric MFA from Classic ASP after LDAP authentication
' 2. Initiating Submit-Button Biometric Authorization for sensitive transactions
' 3. Handling returning user and verifying HMAC signature
' ==============================================================================

Const MFA_GATEWAY_URL = "http://intranet-server/CentralMFA/Gateway.aspx"
Const APP_ID          = "ERP"
Const APP_SECRET      = "ERP_SECRET_KEY_77B1C9D2"

Dim notice, noticeType
notice = ""
noticeType = "info"

' --- Helper function: Compute HMAC-SHA256 in Classic ASP via .NET COM wrapper ---
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
    
    ComputeHmacSha256 = UCase(hexStr)
    On Error GoTo 0
End Function

' --- Helper function: Generate Nonce ---
Function GenerateNonce()
    Dim rndVal, dtStr
    Randomize
    rndVal = CStr(Int((999999 - 100000 + 1) * Rnd + 100000))
    dtStr = Year(Now) & Right("0" & Month(Now), 2) & Right("0" & Day(Now), 2) & Right("0" & Hour(Now), 2) & Right("0" & Minute(Now), 2) & Right("0" & Second(Now), 2)
    GenerateNonce = dtStr & rndVal
End Function

' --- Check if user is returning from CentralMFA Gateway ---
Dim status, retAppId, pcno, action, txnId, ts, nonce, sig, expectedSig, dataToVerify
status = Request.QueryString("status")

If status <> "" Then
    retAppId = Request.QueryString("appId")
    pcno     = Request.QueryString("pcno")
    action   = Request.QueryString("action")
    txnId    = Request.QueryString("txnId")
    ts       = Request.QueryString("timestamp")
    nonce    = Request.QueryString("nonce")
    sig      = Request.QueryString("sig")
    If sig = "" Then sig = Request.QueryString("signature")

    ' Verify HMAC-SHA256 (Canonical format: appId|pcno|status|action|txnId|timestamp|nonce)
    dataToVerify = retAppId & "|" & pcno & "|" & status & "|" & action & "|" & txnId & "|" & ts & "|" & nonce
    expectedSig = ComputeHmacSha256(dataToVerify, APP_SECRET)

    If UCase(sig) <> UCase(expectedSig) Then
        notice = "<strong>Security Error:</strong> HMAC signature mismatch. Tampering detected."
        noticeType = "danger"
    ElseIf UCase(status) = "APPROVED" Then
        noticeType = "success"
        If UCase(action) = "LOGIN" Then
            notice = "<strong>Login Approved!</strong> Biometric MFA verified for PCNO <strong>" & Server.HTMLEncode(pcno) & "</strong>."
        Else
            notice = "<strong>Transaction Approved!</strong> Action: <strong>" & Server.HTMLEncode(action) & "</strong> | TxnRef: <strong>" & Server.HTMLEncode(txnId) & "</strong> | User: <strong>" & Server.HTMLEncode(pcno) & "</strong>."
        End If
    Else
        notice = "<strong>Biometric MFA Denied:</strong> Status: " & Server.HTMLEncode(status) & " for PCNO: " & Server.HTMLEncode(pcno)
        noticeType = "danger"
    End If
End If

' --- Handle Form Post to initiate Gateway Redirect ---
If Request.ServerVariables("REQUEST_METHOD") = "POST" Then
    Dim reqAction, reqPcno, reqTxnId, returnUrl, reqTs, reqNonce, reqData, reqSig, redirectUrl
    reqAction = Request.Form("action_type")
    reqPcno   = Trim(Request.Form("pcno"))
    reqTxnId  = Trim(Request.Form("txn_id"))

    If reqPcno <> "" Then
        returnUrl = "http://" & Request.ServerVariables("HTTP_HOST") & Request.ServerVariables("SCRIPT_NAME")
        reqTs = Year(Now) & Right("0" & Month(Now), 2) & Right("0" & Day(Now), 2) & Right("0" & Hour(Now), 2) & Right("0" & Minute(Now), 2) & Right("0" & Second(Now), 2)
        reqNonce = GenerateNonce()

        reqData = APP_ID & "|" & reqPcno & "|" & returnUrl & "|" & reqAction & "|" & reqTxnId & "|" & reqTs & "|" & reqNonce
        reqSig = ComputeHmacSha256(reqData, APP_SECRET)

        redirectUrl = MFA_GATEWAY_URL & "?appId=" & Server.URLEncode(APP_ID) & _
                      "&pcno=" & Server.URLEncode(reqPcno) & _
                      "&returnUrl=" & Server.URLEncode(returnUrl) & _
                      "&action=" & Server.URLEncode(reqAction) & _
                      "&txnId=" & Server.URLEncode(reqTxnId) & _
                      "&timestamp=" & Server.URLEncode(reqTs) & _
                      "&nonce=" & Server.URLEncode(reqNonce) & _
                      "&signature=" & Server.URLEncode(reqSig)

        Response.Redirect redirectUrl
        Response.End
    End If
End If
%>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8">
    <title>Calling App Demo (Classic ASP) - CentralMFA</title>
    <link href="../Static/app.css" rel="stylesheet" type="text/css">
</head>
<body style="background: #f1f5f9;">
    <header class="app-navbar" style="background: #1e293b;">
        <a href="#" class="brand">
            <span style="font-size: 1.15rem; font-weight: 700; color: #f8fafc;">Legacy Operations Portal (Calling App Demo)</span>
            <span class="brand-badge" style="background: #334155; color: #94a3b8;">Classic ASP</span>
        </a>
        <div style="font-size: 0.82rem; color: #94a3b8;">
            AppId: <strong>LEGACY_APP</strong>
        </div>
    </header>

    <main class="main-content" style="max-width: 860px; margin: 36px auto; padding: 0 16px;">
        <% If notice <> "" Then %>
            <div class="alert alert-<%= noticeType %>" style="margin-bottom: 24px;">
                <%= notice %>
            </div>
        <% End If %>

        <!-- Scenario 1: Post-LDAP Login MFA -->
        <div class="card" style="margin-bottom: 24px;">
            <div class="card-header">
                <h2 class="card-title">Scenario 1: Post-LDAP Login Biometric MFA (Classic ASP)</h2>
                <p class="card-desc">
                    Employee authenticates in legacy Classic ASP login page. Server redirects to CentralMFA for biometric step-up.
                </p>
            </div>
            <form method="POST" action="">
                <input type="hidden" name="action_type" value="LOGIN">
                <div class="form-group">
                    <label class="form-label">Employee PCNO (from LDAP)</label>
                    <input type="text" name="pcno" class="form-control" value="EMP003" style="max-width: 320px;" required>
                </div>
                <button type="submit" class="btn btn-primary">Simulate LDAP Login &amp; Redirect to MFA Gateway</button>
            </form>
        </div>

        <!-- Scenario 2: Sensitive Button Click / Transaction Authorization -->
        <div class="card">
            <div class="card-header">
                <h2 class="card-title">Scenario 2: Sensitive Submit Button Authorization (Classic ASP)</h2>
                <p class="card-desc">
                    Clerk submits stock ledger update or journal entry. Requires supervisor fingerprint authorization before updating legacy database.
                </p>
            </div>
            <form method="POST" action="">
                <input type="hidden" name="action_type" value="APPROVE_LEDGER">
                <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin-bottom: 16px;">
                    <div class="form-group">
                        <label class="form-label">Transaction Reference (TxnId)</label>
                        <input type="text" name="txn_id" class="form-control" value="GL-2026-4401" required>
                    </div>
                    <div class="form-group">
                        <label class="form-label">Supervisor PCNO</label>
                        <input type="text" name="pcno" class="form-control" value="EMP003" required>
                    </div>
                </div>
                <button type="submit" class="btn btn-secondary">Authorize Ledger Entry with Fingerprint</button>
            </form>
        </div>
    </main>
</body>
</html>
