<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="AuditLogs.aspx.cs" Inherits="CentralMFA.Admin.AuditLogs" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Biometric Audit Logs - CentralMFA</title>
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
                <span class="brand-badge">Audit Trail</span>
            </a>
            <nav class="nav-links">
                <a href="Dashboard.aspx" class="nav-link">Overview</a>
                <a href="Apps.aspx" class="nav-link">Applications</a>
                <a href="Policies.aspx" class="nav-link">Policies</a>
                <a href="UserMapping.aspx" class="nav-link">User Mapping</a>
                <a href="AuditLogs.aspx" class="nav-link active">Audit Logs</a>
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
                        <h1 style="font-size: 1.45rem; font-weight: 700; color: var(--gray-900);">Biometric Verification Audit Trail</h1>
                        <p style="font-size: 0.88rem; color: var(--gray-500); margin-top: 2px;">
                            Complete security log of every biometric login and transaction authorization attempt across your intranet
                        </p>
                    </div>

                    <div>
                        <asp:Button ID="btnRefresh" runat="server" Text="Refresh Logs" CssClass="btn btn-secondary btn-sm" OnClick="btnRefresh_Click" />
                    </div>
                </div>

                <div class="data-table-wrapper">
                    <asp:Repeater ID="rptLogs" runat="server">
                        <HeaderTemplate>
                            <table class="data-table">
                                <thead>
                                    <tr>
                                        <th>Log ID</th>
                                        <th>Timestamp</th>
                                        <th>Application</th>
                                        <th>PCNO</th>
                                        <th>Action Type</th>
                                        <th>Txn Ref</th>
                                        <th>Match Score</th>
                                        <th>Status</th>
                                        <th>Client IP</th>
                                        <th>Details</th>
                                    </tr>
                                </thead>
                                <tbody>
                        </HeaderTemplate>
                        <ItemTemplate>
                            <tr>
                                <td style="font-size: 0.78rem; color: var(--gray-500);"><%# Eval("LogId") %></td>
                                <td style="font-size: 0.8rem; color: var(--gray-600);"><%# Eval("CreatedOn", "{0:yyyy-MM-dd HH:mm:ss}") %></td>
                                <td><strong style="color: var(--primary);"><%# Eval("AppId") %></strong></td>
                                <td><strong><%# Eval("PcNo") %></strong></td>
                                <td><span class="badge badge-gray"><%# Eval("ActionName") %></span></td>
                                <td style="font-size: 0.8rem;"><%# Eval("TxnId") %></td>
                                <td><%# Convert.ToInt32(Eval("MatchScore")) > 0 ? "<strong>" + Eval("MatchScore") + "</strong>" : "-" %></td>
                                <td>
                                    <%# Eval("Status").ToString() == "APPROVED" ? "<span class='badge badge-success'>APPROVED</span>" : (Eval("Status").ToString() == "DENIED" ? "<span class='badge badge-danger'>DENIED</span>" : "<span class='badge badge-warning'>" + Eval("Status") + "</span>") %>
                                </td>
                                <td style="font-size: 0.8rem;"><%# Eval("ClientIp") %></td>
                                <td style="font-size: 0.78rem; color: var(--gray-600); max-width: 200px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;"><%# Eval("Details") %></td>
                            </tr>
                        </ItemTemplate>
                        <FooterTemplate>
                                </tbody>
                            </table>
                        </FooterTemplate>
                    </asp:Repeater>

                    <asp:Panel ID="pnlNoLogs" runat="server" Visible="false" style="padding: 28px; text-align: center; color: var(--gray-500);">
                        No audit events recorded yet.
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
