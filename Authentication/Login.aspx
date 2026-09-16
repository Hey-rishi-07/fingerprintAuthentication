<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="Authentication.Login" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>MFA Login - Active Directory + Mantra Fingerprint</title>
    <link href="Static/app.css" rel="stylesheet" type="text/css" />
</head>
<body>
    <form id="form1" runat="server">
        <!-- Navigation Bar -->
        <header class="app-navbar">
            <a href="Login.aspx" class="brand">
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#2563eb" stroke-width="2">
                    <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
                    <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
                </svg>
                <span>MFA Portal</span>
                <span class="brand-badge">Offline MFA</span>
            </a>
            <nav class="nav-links">
                <a href="Login.aspx" class="nav-link active">MFA Login</a>
                <a href="Enroll.aspx" class="nav-link">Fingerprint Enrollment</a>
            </nav>
        </header>

        <!-- Main Content -->
        <main class="main-content">
            <div class="auth-card">
                <div class="card-header">
                    <h1 class="card-title">Two-Factor Authentication</h1>
                    <p class="card-subtitle">Active Directory credentials + Mantra MFS100 fingerprint verification</p>
                </div>

                <!-- Stepper Progress Indicator -->
                <div class="stepper">
                    <div id="stepIndicator1" runat="server" class="step-item active">
                        <div class="step-bubble">1</div>
                        <span class="step-label">AD Login</span>
                    </div>
                    <div id="stepIndicator2" runat="server" class="step-item">
                        <div class="step-bubble">2</div>
                        <span class="step-label">Fingerprint</span>
                    </div>
                </div>

                <!-- Demo Mode Toggle -->
                <div class="demo-toggle">
                    <label class="demo-toggle-label" for="chkDemoMode">Demo Mode (No physical scanner required)</label>
                    <asp:CheckBox ID="chkDemoMode" runat="server" AutoPostBack="true" OnCheckedChanged="chkDemoMode_CheckedChanged" />
                </div>

                <!-- Status / Error Notification -->
                <asp:Panel ID="pnlStatus" runat="server" Visible="false">
                    <div id="statusBox" runat="server" class="alert alert-danger">
                        <asp:Literal ID="litStatusMessage" runat="server" />
                    </div>
                </asp:Panel>

                <!-- STEP 1: Active Directory Credentials -->
                <asp:Panel ID="pnlStep1" runat="server">
                    <div class="form-group">
                        <label class="form-label" for="txtUsername">Active Directory Username / PCNO</label>
                        <asp:TextBox ID="txtUsername" runat="server" CssClass="form-control" placeholder="e.g. 1001, 1002, or username" autocomplete="username" />
                        <small style="color: var(--gray-500); font-size: 0.78rem; margin-top: 4px; display: block;">
                            Offline test bypass accounts: <strong>1001</strong>, <strong>1002</strong>, <strong>admin</strong>
                        </small>
                    </div>

                    <div class="form-group">
                        <label class="form-label" for="txtPassword">Active Directory Password</label>
                        <asp:TextBox ID="txtPassword" runat="server" CssClass="form-control" TextMode="Password" placeholder="Enter your domain password" autocomplete="current-password" />
                    </div>

                    <asp:Button ID="btnVerifyAD" runat="server" Text="Next: Verify Active Directory" CssClass="btn btn-primary" OnClick="btnVerifyAD_Click" />
                </asp:Panel>

                <!-- STEP 2: Fingerprint Verification (MFA) -->
                <asp:Panel ID="pnlStep2" runat="server" Visible="false">
                    <div class="alert alert-info" style="margin-bottom: 16px;">
                        <strong>AD Factor Passed!</strong><br />
                        Employee ID / PCNO: <strong><asp:Literal ID="litAuthenticatedPcNo" runat="server" /></strong>
                    </div>

                    <div class="scan-container">
                        <div class="scan-icon-wrapper">
                            <svg class="scan-icon" viewBox="0 0 24 24" fill="currentColor">
                                <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z"/>
                            </svg>
                        </div>
                        <h3 style="font-size: 1.05rem; font-weight: 700; color: var(--gray-800); margin-bottom: 4px;">Place Registered Finger on Scanner</h3>
                        <p class="scan-meta">Mantra MFS100 scanner will compare against the Oracle 11g template for this PCNO.</p>
                        <p id="lblDeviceStatus" runat="server" style="font-size: 0.8rem; margin-top: 8px; color: var(--primary); font-weight: 600;"></p>
                    </div>

                    <div class="btn-group" style="flex-direction: column;">
                        <asp:Button ID="btnScanAndVerify" runat="server" Text="Scan Fingerprint & Complete Login" CssClass="btn btn-primary" OnClick="btnScanAndVerify_Click" />
                        <asp:Button ID="btnBackToStep1" runat="server" Text="Cancel / Switch Account" CssClass="btn btn-secondary" OnClick="btnBackToStep1_Click" />
                    </div>

                    <div style="text-align: center; margin-top: 18px;">
                        <span style="font-size: 0.82rem; color: var(--gray-500);">Fingerprint not registered yet? </span>
                        <a href="Enroll.aspx" style="font-size: 0.82rem; color: var(--primary); font-weight: 600; text-decoration: none;">Enroll in Oracle Database</a>
                    </div>
                </asp:Panel>
            </div>
        </main>

        <!-- Footer -->
        <footer class="app-footer">
            <span>MFA Authentication Portal • Active Directory + Mantra MFS100 + Oracle 11g • 100% Intranet Offline</span>
        </footer>
    </form>
</body>
</html>
