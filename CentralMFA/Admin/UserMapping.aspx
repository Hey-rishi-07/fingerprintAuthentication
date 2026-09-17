<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="UserMapping.aspx.cs" Inherits="CentralMFA.Admin.UserMapping" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Specific User Policy Mapping - CentralMFA</title>
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
                <span class="brand-badge">User Mapping</span>
            </a>
            <nav class="nav-links">
                <a href="Dashboard.aspx" class="nav-link">Overview</a>
                <a href="Apps.aspx" class="nav-link">Applications</a>
                <a href="Policies.aspx" class="nav-link">Policies</a>
                <a href="UserMapping.aspx" class="nav-link active">User Mapping</a>
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
                    <h1 style="font-size: 1.45rem; font-weight: 700; color: var(--gray-900);">Specific User MFA Mapping</h1>
                    <p style="font-size: 0.88rem; color: var(--gray-500); margin-top: 2px;">
                        When an application is configured for <code>SPECIFIC_USERS</code> policy, only the PCNOs listed below must scan fingerprints
                    </p>
                </div>

                <asp:Panel ID="pnlStatus" runat="server" Visible="false">
                    <div id="statusBox" runat="server" class="alert alert-info">
                        <asp:Literal ID="litStatusMessage" runat="server" />
                    </div>
                </asp:Panel>

                <div class="auth-card card-wide" style="padding: 24px; margin-bottom: 24px;">
                    <div style="display: flex; gap: 16px; align-items: flex-end; flex-wrap: wrap;">
                        <div style="flex: 1; min-width: 260px;">
                            <label class="form-label" for="ddlApps">Target Application</label>
                            <asp:DropDownList ID="ddlApps" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlApps_SelectedIndexChanged" />
                        </div>

                        <div style="flex: 1; min-width: 220px;">
                            <label class="form-label" for="txtAddPcno">Add Employee ID / PCNO to Policy</label>
                            <asp:TextBox ID="txtAddPcno" runat="server" CssClass="form-control" placeholder="e.g. 1001, 1002" />
                        </div>

                        <div>
                            <asp:Button ID="btnAddUser" runat="server" Text="+ Add PCNO to List" CssClass="btn btn-primary btn-sm" OnClick="btnAddUser_Click" />
                        </div>
                    </div>
                </div>

                <!-- Whitelisted Users Table -->
                <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px;">
                    <h3 style="font-size: 1.1rem; font-weight: 700; color: var(--gray-900);">
                        Designated Mandatory Users for: <asp:Literal ID="litAppName" runat="server">Application</asp:Literal>
                    </h3>
                </div>

                <div class="data-table-wrapper">
                    <asp:Repeater ID="rptWhitelistedUsers" runat="server" OnItemCommand="rptWhitelistedUsers_ItemCommand">
                        <HeaderTemplate>
                            <table class="data-table">
                                <thead>
                                    <tr>
                                        <th>Mandatory PCNO</th>
                                        <th>Application ID</th>
                                        <th>Fingerprint Enrolled?</th>
                                        <th>Action</th>
                                    </tr>
                                </thead>
                                <tbody>
                        </HeaderTemplate>
                        <ItemTemplate>
                            <tr>
                                <td><strong style="font-size: 0.95rem;"><%# Container.DataItem %></strong></td>
                                <td><span class="badge badge-info"><%= ddlApps.SelectedValue %></span></td>
                                <td>
                                    <%# CentralMFA.Utils.DBHelper.HasFingerprint(Container.DataItem.ToString()) ? "<span class='badge badge-success'>ENROLLED</span>" : "<span class='badge badge-warning'>NOT ENROLLED</span>" %>
                                </td>
                                <td>
                                    <asp:LinkButton ID="btnRemove" runat="server" CommandName="RemoveUser" CommandArgument='<%# Container.DataItem %>'
                                        CssClass="btn btn-secondary btn-sm" Text="Remove from MFA List" OnClientClick="return confirm('Remove this PCNO from the mandatory list?');" />
                                </td>
                            </tr>
                        </ItemTemplate>
                        <FooterTemplate>
                                </tbody>
                            </table>
                        </FooterTemplate>
                    </asp:Repeater>

                    <asp:Panel ID="pnlNoUsers" runat="server" Visible="false" style="padding: 24px; text-align: center; color: var(--gray-500); font-size: 0.88rem;">
                        No users currently assigned to this application's specific list. (If policy is SPECIFIC_USERS, everyone currently bypasses).
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
