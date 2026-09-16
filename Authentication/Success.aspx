<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Success.aspx.cs" Inherits="Authentication.Success" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Login Successful - MFA Verified</title>
    <link href="Static/app.css" rel="stylesheet" type="text/css" />
</head>
<body>
    <form id="form1" runat="server">
        <header class="app-navbar">
            <a href="Login.aspx" class="brand">
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#16a34a" stroke-width="2">
                    <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
                    <polyline points="22 4 12 14.01 9 11.01"></polyline>
                </svg>
                <span>MFA Portal</span>
                <span class="brand-badge" style="background: var(--success);">Authenticated</span>
            </a>
            <nav class="nav-links">
                <a href="Login.aspx" class="nav-link">MFA Login</a>
                <a href="Enroll.aspx" class="nav-link">Fingerprint Enrollment</a>
            </nav>
        </header>

        <main class="main-content">
            <div class="auth-card" style="text-align: center;">
                <div style="width: 72px; height: 72px; border-radius: 50%; background: var(--success-bg); border: 2px solid var(--success-border); display: flex; align-items: center; justify-content: center; margin: 0 auto 16px;">
                    <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="#16a34a" stroke-width="2.5">
                        <polyline points="20 6 9 17 4 12"></polyline>
                    </svg>
                </div>

                <h1 class="card-title" style="color: var(--success); margin-bottom: 4px;">Login Successful!</h1>
                <p class="card-subtitle" style="margin-bottom: 24px;">Active Directory & Fingerprint MFA Verified</p>

                <div style="background: var(--gray-50); border: 1px solid var(--gray-200); border-radius: 8px; padding: 16px; text-align: left; margin-bottom: 24px;">
                    <div style="display: flex; justify-content: space-between; padding: 6px 0; border-bottom: 1px solid var(--gray-200); font-size: 0.88rem;">
                        <span style="color: var(--gray-500);">Employee ID / PCNO:</span>
                        <strong style="color: var(--gray-900);"><asp:Literal ID="litPcNo" runat="server">1001</asp:Literal></strong>
                    </div>
                    <div style="display: flex; justify-content: space-between; padding: 6px 0; border-bottom: 1px solid var(--gray-200); font-size: 0.88rem;">
                        <span style="color: var(--gray-500);">Factor 1 (Active Directory):</span>
                        <span class="badge badge-success">PASSED</span>
                    </div>
                    <div style="display: flex; justify-content: space-between; padding: 6px 0; border-bottom: 1px solid var(--gray-200); font-size: 0.88rem;">
                        <span style="color: var(--gray-500);">Factor 2 (Fingerprint Match Score):</span>
                        <strong style="color: var(--primary);"><asp:Literal ID="litMatchScore" runat="server">3850</asp:Literal></strong>
                    </div>
                    <div style="display: flex; justify-content: space-between; padding: 6px 0; border-bottom: 1px solid var(--gray-200); font-size: 0.88rem;">
                        <span style="color: var(--gray-500);">Database Source:</span>
                        <strong style="color: var(--gray-700);">Oracle 11g (APP_USER_FINGERPRINTS)</strong>
                    </div>
                    <div style="display: flex; justify-content: space-between; padding: 6px 0; border-bottom: 1px solid var(--gray-200); font-size: 0.88rem;">
                        <span style="color: var(--gray-500);">Verification Mode:</span>
                        <span class="badge badge-info"><asp:Literal ID="litMode" runat="server">Demo Mode</asp:Literal></span>
                    </div>
                    <div style="display: flex; justify-content: space-between; padding: 6px 0; font-size: 0.88rem;">
                        <span style="color: var(--gray-500);">Authentication Timestamp:</span>
                        <span style="color: var(--gray-700);"><asp:Literal ID="litTimestamp" runat="server"></asp:Literal></span>
                    </div>
                </div>

                <div class="btn-group" style="flex-direction: column;">
                    <asp:Button ID="btnLogout" runat="server" Text="Test Another Login (Logout)" CssClass="btn btn-primary" OnClick="btnLogout_Click" />
                    <a href="Enroll.aspx" class="btn btn-secondary">Go to Fingerprint Enrollment</a>
                </div>
            </div>
        </main>

        <footer class="app-footer">
            <span>MFA Authentication Portal • Active Directory + Mantra MFS100 + Oracle 11g • 100% Intranet Offline</span>
        </footer>
    </form>
</body>
</html>
