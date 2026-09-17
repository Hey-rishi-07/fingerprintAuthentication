<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Dashboard.aspx.cs" Inherits="CentralMFA.Admin.Dashboard" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Admin Dashboard - CentralMFA Management</title>
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
                <span class="brand-badge">Admin Dashboard</span>
            </a>
            <nav class="nav-links">
                <a href="Dashboard.aspx" class="nav-link active">Overview</a>
                <a href="Apps.aspx" class="nav-link">Applications</a>
                <a href="Policies.aspx" class="nav-link">Policies</a>
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
                <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px;">
                    <div>
                        <h1 style="font-size: 1.5rem; font-weight: 700; color: var(--gray-900);">Enterprise MFA Management</h1>
                        <p style="font-size: 0.88rem; color: var(--gray-500); margin-top: 2px;">
                            Centralized policy control for intranet applications and Mantra MFS100 biometric verifications
                        </p>
                    </div>
                    <div style="display: flex; gap: 8px;">
                        <a href="../Gateway.aspx" class="btn btn-secondary btn-sm" target="_blank">Test Gateway Screen</a>
                        <a href="Apps.aspx" class="btn btn-primary btn-sm">+ Register New App</a>
                    </div>
                </div>

                <!-- Metrics Grid -->
                <div class="metrics-grid">
                    <div class="metric-card">
                        <div class="metric-title">Registered Apps</div>
                        <div class="metric-value"><asp:Literal ID="litTotalApps" runat="server">3</asp:Literal></div>
                        <div class="metric-desc"><asp:Literal ID="litActiveApps" runat="server">3</asp:Literal> Active in Intranet</div>
                    </div>

                    <div class="metric-card">
                        <div class="metric-title">Enrolled Biometric Users</div>
                        <div class="metric-value"><asp:Literal ID="litEnrolledUsers" runat="server">0</asp:Literal></div>
                        <div class="metric-desc">Stored in Oracle 11g (BLOB)</div>
                    </div>

                    <div class="metric-card">
                        <div class="metric-title">Verifications Today</div>
                        <div class="metric-value" style="color: var(--primary);"><asp:Literal ID="litTodayTotal" runat="server">0</asp:Literal></div>
                        <div class="metric-desc">Total attempts across all apps</div>
                    </div>

                    <div class="metric-card">
                        <div class="metric-title">Approved Today</div>
                        <div class="metric-value" style="color: var(--success);"><asp:Literal ID="litTodayApproved" runat="server">0</asp:Literal></div>
                        <div class="metric-desc">Passed Biometric / Policy checks</div>
                    </div>

                    <div class="metric-card">
                        <div class="metric-title">Denied Today</div>
                        <div class="metric-value" style="color: var(--danger);"><asp:Literal ID="litTodayDenied" runat="server">0</asp:Literal></div>
                        <div class="metric-desc">Failed fingerprint matches</div>
                    </div>
                </div>

                <!-- Client Agent Deployment Banner -->
                <div class="action-banner action-highlight" style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px;">
                    <div>
                        <div class="action-label">Client PC Scanner Deployment Package</div>
                        <div class="action-title">Mantra MFS100 Scanner Agent (Port 8034)</div>
                        <div class="action-ref">Deploy this lightweight agent to employee PCs so their browsers can talk to their USB scanners.</div>
                    </div>
                    <div style="display: flex; gap: 10px; align-items: center;">
                        <a href="../DownloadAgent.ashx" class="btn btn-primary" style="text-decoration: none; display: inline-flex; align-items: center; gap: 6px; font-weight: 600;">
                            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
                                <polyline points="7 10 12 15 17 10"></polyline>
                                <line x1="12" y1="15" x2="12" y2="3"></line>
                            </svg>
                            Download Agent Setup (ZIP)
                        </a>
                    </div>
                </div>

                <!-- Registered Applications Table -->
                <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px;">
                    <h3 style="font-size: 1.15rem; font-weight: 700; color: var(--gray-900);">Registered Intranet Applications</h3>
                    <a href="Apps.aspx" style="font-size: 0.84rem; color: var(--primary); font-weight: 600; text-decoration: none;">Manage Applications &rarr;</a>
                </div>

                <div class="data-table-wrapper">
                    <asp:Repeater ID="rptApps" runat="server">
                        <HeaderTemplate>
                            <table class="data-table">
                                <thead>
                                    <tr>
                                        <th>App ID</th>
                                        <th>Application Name</th>
                                        <th>Enforced Policy</th>
                                        <th>Callback / Return URL</th>
                                        <th>Status</th>
                                        <th>Actions</th>
                                    </tr>
                                </thead>
                                <tbody>
                        </HeaderTemplate>
                        <ItemTemplate>
                            <tr>
                                <td><strong style="color: var(--primary);"><%# Eval("AppId") %></strong></td>
                                <td><strong><%# Eval("AppName") %></strong></td>
                                <td>
                                    <span class="badge badge-info"><%# Eval("PolicyType") %></span>
                                </td>
                                <td style="font-size: 0.8rem; color: var(--gray-600); max-width: 300px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;">
                                    <%# Eval("CallbackUrl") %>
                                </td>
                                <td>
                                    <%# Convert.ToBoolean(Eval("IsActive")) ? "<span class='badge badge-success'>ACTIVE</span>" : "<span class='badge badge-danger'>DISABLED</span>" %>
                                </td>
                                <td>
                                    <a href='Policies.aspx?appId=<%# Eval("AppId") %>' class="btn btn-secondary btn-sm">Edit Policy</a>
                                    <a href='../Gateway.aspx?appId=<%# Eval("AppId") %>&pcno=1001' target="_blank" class="btn btn-secondary btn-sm">Test</a>
                                </td>
                            </tr>
                        </ItemTemplate>
                        <FooterTemplate>
                                </tbody>
                            </table>
                        </FooterTemplate>
                    </asp:Repeater>
                </div>

                <!-- Recent Verification Events -->
                <div style="display: flex; justify-content: space-between; align-items: center; margin-top: 32px; margin-bottom: 12px;">
                    <h3 style="font-size: 1.15rem; font-weight: 700; color: var(--gray-900);">Recent Authentication & Action Events</h3>
                    <a href="AuditLogs.aspx" style="font-size: 0.84rem; color: var(--primary); font-weight: 600; text-decoration: none;">View Complete Audit Trail &rarr;</a>
                </div>

                <div class="data-table-wrapper">
                    <asp:Repeater ID="rptAudit" runat="server">
                        <HeaderTemplate>
                            <table class="data-table">
                                <thead>
                                    <tr>
                                        <th>Time</th>
                                        <th>Application</th>
                                        <th>PCNO</th>
                                        <th>Action Type</th>
                                        <th>Txn Ref</th>
                                        <th>Match Score</th>
                                        <th>Outcome</th>
                                    </tr>
                                </thead>
                                <tbody>
                        </HeaderTemplate>
                        <ItemTemplate>
                            <tr>
                                <td style="font-size: 0.8rem; color: var(--gray-600);"><%# Eval("CreatedOn", "{0:HH:mm:ss}") %></td>
                                <td><strong><%# Eval("AppId") %></strong></td>
                                <td><%# Eval("PcNo") %></td>
                                <td><span class="badge badge-gray"><%# Eval("ActionName") %></span></td>
                                <td style="font-size: 0.8rem;"><%# Eval("TxnId") %></td>
                                <td><strong><%# Eval("MatchScore") %></strong></td>
                                <td>
                                    <%# Eval("Status").ToString() == "APPROVED" ? "<span class='badge badge-success'>APPROVED</span>" : (Eval("Status").ToString() == "DENIED" ? "<span class='badge badge-danger'>DENIED</span>" : "<span class='badge badge-warning'>" + Eval("Status") + "</span>") %>
                                </td>
                            </tr>
                        </ItemTemplate>
                        <FooterTemplate>
                                </tbody>
                            </table>
                        </FooterTemplate>
                    </asp:Repeater>

                    <asp:Panel ID="pnlNoAudit" runat="server" Visible="false" style="padding: 24px; text-align: center; color: var(--gray-500); font-size: 0.88rem;">
                        No verification events recorded today yet. Run a test from Gateway.aspx!
                    </asp:Panel>
                </div>
            </div>
        </main>

        <footer class="app-footer">
            <span>CentralMFA Enterprise Gateway &bull; Active Directory + Mantra MFS100 + Oracle 11g</span>
        </footer>
    </form>
</body>
</html>
