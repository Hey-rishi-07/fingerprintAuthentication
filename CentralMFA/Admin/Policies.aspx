<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Policies.aspx.cs" Inherits="CentralMFA.Admin.Policies" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>MFA Policy Enforcement - CentralMFA</title>
    <link href="../Static/app.css" rel="stylesheet" type="text/css" />
</head>
<body>
    <form id="form1" runat="server">
        <header class="app-navbar">
            <a href="Dashboard.aspx" class="brand">
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#2563eb" stroke-width="2">
                    <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
                    <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
                </svg>
                <span>CentralMFA</span>
                <span class="brand-badge">Policy Control</span>
            </a>
            <nav class="nav-links">
                <a href="Dashboard.aspx" class="nav-link">Overview</a>
                <a href="Apps.aspx" class="nav-link">Applications</a>
                <a href="Policies.aspx" class="nav-link active">Policies</a>
                <a href="UserMapping.aspx" class="nav-link">User Mapping</a>
                <a href="AuditLogs.aspx" class="nav-link">Audit Logs</a>
                <a href="../Enroll.aspx" class="nav-link">Biometric Enrollment</a>
                <a href="../DownloadAgent.ashx" class="nav-link" style="color: #2563eb; font-weight: 600;" title="Download Client Agent Setup Package">
                    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="vertical-align: -2px; margin-right: 3px;">
                        <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
                        <polyline points="7 10 12 15 17 10"></polyline>
                        <line x1="12" y1="15" x2="12" y2="3"></line>
                    </svg>
                    Download Agent (ZIP)
                </a>
                <asp:LinkButton ID="btnLogout" runat="server" CssClass="nav-link" Text="Sign Out" OnClick="btnLogout_Click" />
            </nav>
        </header>

        <main class="main-content admin-layout" style="display: block;">
            <div class="content-wide">
                <div style="margin-bottom: 24px;">
                    <h1 style="font-size: 1.45rem; font-weight: 700; color: var(--gray-900);">Biometric MFA Policy Rules</h1>
                    <p style="font-size: 0.88rem; color: var(--gray-500); margin-top: 2px;">
                        Configure whether an application demands fingerprints from all employees or only specific privileged users
                    </p>
                </div>

                <asp:Panel ID="pnlStatus" runat="server" Visible="false">
                    <div id="statusBox" runat="server" class="alert alert-info">
                        <asp:Literal ID="litStatusMessage" runat="server" />
                    </div>
                </asp:Panel>

                <div style="display: grid; grid-template-columns: 1.2fr 1fr; gap: 24px;">
                    <!-- Policy Editor Form -->
                    <div class="auth-card" style="width: 100%; max-width: none; padding: 24px;">
                        <h3 style="font-size: 1.1rem; font-weight: 700; color: var(--gray-900); margin-bottom: 16px;">Configure Application Policy</h3>

                        <div class="form-group">
                            <label class="form-label" for="ddlApps">Select Application</label>
                            <asp:DropDownList ID="ddlApps" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlApps_SelectedIndexChanged" />
                        </div>

                        <div class="form-group">
                            <label class="form-label" for="rblPolicy">Enforcement Mode</label>
                            <asp:RadioButtonList ID="rblPolicy" runat="server" CssClass="form-control" style="border: none; padding: 0;">
                                <asp:ListItem Text=" ALL_USERS: Mandatory for every user logging into or submitting in this app" Value="ALL_USERS" />
                                <asp:ListItem Text=" SPECIFIC_USERS: Only ask fingerprint from designated PCNOs; others bypass automatically" Value="SPECIFIC_USERS" />
                                <asp:ListItem Text=" ENROLLED_ONLY: Only ask fingerprint if user has enrolled; un-enrolled users bypass" Value="ENROLLED_ONLY" />
                                <asp:ListItem Text=" DISABLED: Temporarily disable biometric checks for this app (immediate approval)" Value="DISABLED" />
                            </asp:RadioButtonList>
                        </div>

                        <div style="margin-top: 20px;">
                            <asp:Button ID="btnSavePolicy" runat="server" Text="Save Policy Rule" CssClass="btn btn-primary" OnClick="btnSavePolicy_Click" />
                        </div>

                        <div id="pnlUserMappingLink" runat="server" style="margin-top: 16px; padding: 12px; background: var(--primary-light); border-radius: 6px;">
                            <span style="font-size: 0.84rem; color: var(--primary);">Need to specify which PCNOs are subject to this policy?</span><br />
                            <asp:HyperLink ID="lnkUserMapping" runat="server" NavigateUrl="UserMapping.aspx" style="font-weight: 700; font-size: 0.88rem; color: var(--primary); text-decoration: underline;">
                                Manage Specific Users Whitelist for this Application &rarr;
                            </asp:HyperLink>
                        </div>
                    </div>

                    <!-- Policy Explanations Reference -->
                    <div style="background: #ffffff; border: 1px solid var(--gray-200); border-radius: var(--radius-md); padding: 24px;">
                        <h4 style="font-size: 0.95rem; font-weight: 700; color: var(--gray-900); margin-bottom: 12px;">Policy Mode Reference Guide</h4>

                        <div style="margin-bottom: 16px;">
                            <span class="badge badge-info" style="margin-bottom: 4px;">ALL_USERS (Default)</span>
                            <p style="font-size: 0.82rem; color: var(--gray-600); line-height: 1.4;">
                                Highest security. Every single employee who logs in or performs an action must place their finger on the Mantra scanner.
                            </p>
                        </div>

                        <div style="margin-bottom: 16px;">
                            <span class="badge badge-warning" style="margin-bottom: 4px;">SPECIFIC_USERS</span>
                            <p style="font-size: 0.82rem; color: var(--gray-600); line-height: 1.4;">
                                Selective security. The gateway checks if the user's PCNO is registered in the whitelist. If yes, requires fingerprint. If no, immediately approves and redirects back without disturbing regular users.
                            </p>
                        </div>

                        <div style="margin-bottom: 16px;">
                            <span class="badge badge-success" style="margin-bottom: 4px;">ENROLLED_ONLY</span>
                            <p style="font-size: 0.82rem; color: var(--gray-600); line-height: 1.4;">
                                Phased rollout mode. If the employee has enrolled their fingerprint in Oracle 11g, they must scan it. If they haven't enrolled yet, the gateway bypasses them.
                            </p>
                        </div>

                        <div>
                            <span class="badge badge-danger" style="margin-bottom: 4px;">DISABLED</span>
                            <p style="font-size: 0.82rem; color: var(--gray-600); line-height: 1.4;">
                                Maintenance mode. Bypasses biometric checks and returns an instant approval signature without user prompt.
                            </p>
                        </div>
                    </div>
                </div>
            </div>
        </main>

        <footer class="app-footer">
            <span>CentralMFA Enterprise Gateway &bull; Active Directory + Mantra MFS100 + Oracle 11g</span>
        </footer>
    </form>
</body>
</html>
