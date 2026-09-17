<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="CentralMFA.Admin.Login" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Admin Login - CentralMFA Management</title>
    <link href="../Static/app.css" rel="stylesheet" type="text/css" />
</head>
<body>
    <form id="form1" runat="server">
        <header class="app-navbar">
            <a href="../Gateway.aspx" class="brand">
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#2563eb" stroke-width="2">
                    <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
                    <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
                </svg>
                <span>CentralMFA</span>
                <span class="brand-badge">Admin Portal</span>
            </a>
            <nav class="nav-links">
                <a href="../Gateway.aspx" class="nav-link">Gateway Test</a>
                <a href="../Enroll.aspx" class="nav-link">Fingerprint Enrollment</a>
            </nav>
        </header>

        <main class="main-content">
            <div class="auth-card" style="max-width: 420px;">
                <div class="card-header">
                    <h1 class="card-title">Administrator Login</h1>
                    <p class="card-subtitle">Manage registered applications, MFA policies, and audit logs</p>
                </div>

                <asp:Panel ID="pnlStatus" runat="server" Visible="false">
                    <div id="statusBox" runat="server" class="alert alert-danger">
                        <asp:Literal ID="litStatusMessage" runat="server" />
                    </div>
                </asp:Panel>

                <div class="form-group">
                    <label class="form-label" for="txtUsername">Username</label>
                    <asp:TextBox ID="txtUsername" runat="server" CssClass="form-control" Text="admin" />
                </div>

                <div class="form-group">
                    <label class="form-label" for="txtPassword">Password</label>
                    <asp:TextBox ID="txtPassword" runat="server" CssClass="form-control" TextMode="Password" placeholder="Default: admin123" />
                </div>

                <div style="margin-top: 24px;">
                    <asp:Button ID="btnLogin" runat="server" Text="Sign In to Admin Portal" CssClass="btn btn-primary" OnClick="btnLogin_Click" />
                </div>

                <p style="text-align: center; font-size: 0.78rem; color: var(--gray-500); margin-top: 16px;">
                    Credentials configured in Web.config (Default: admin / admin123)
                </p>
            </div>
        </main>

        <footer class="app-footer">
            <span>CentralMFA Enterprise Gateway &bull; Active Directory + Mantra MFS100 + Oracle 11g</span>
        </footer>
    </form>
</body>
</html>
