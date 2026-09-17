<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Apps.aspx.cs" Inherits="CentralMFA.Admin.Apps" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Applications Registry - CentralMFA</title>
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
                <span class="brand-badge">App Registry</span>
            </a>
            <nav class="nav-links">
                <a href="Dashboard.aspx" class="nav-link">Overview</a>
                <a href="Apps.aspx" class="nav-link active">Applications</a>
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
                <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px;">
                    <div>
                        <h1 style="font-size: 1.45rem; font-weight: 700; color: var(--gray-900);">Intranet Applications Registry</h1>
                        <p style="font-size: 0.88rem; color: var(--gray-500); margin-top: 2px;">
                            Configure intranet applications allowed to delegate authentication to this biometric gateway
                        </p>
                    </div>
                </div>

                <asp:Panel ID="pnlStatus" runat="server" Visible="false">
                    <div id="statusBox" runat="server" class="alert alert-info">
                        <asp:Literal ID="litStatusMessage" runat="server" />
                    </div>
                </asp:Panel>

                <!-- Registration / Edit Form -->
                <div class="auth-card card-wide" style="padding: 24px; margin-bottom: 30px;">
                    <h3 style="font-size: 1.1rem; font-weight: 700; color: var(--gray-900); margin-bottom: 16px;">
                        <asp:Literal ID="litFormTitle" runat="server">Register New Application</asp:Literal>
                    </h3>

                    <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 16px;">
                        <div class="form-group">
                            <label class="form-label" for="txtAppId">App ID (Unique Key)</label>
                            <asp:TextBox ID="txtAppId" runat="server" CssClass="form-control" placeholder="e.g. HRMS, ERP, PAYROLL" />
                        </div>

                        <div class="form-group">
                            <label class="form-label" for="txtAppName">Application Display Name</label>
                            <asp:TextBox ID="txtAppName" runat="server" CssClass="form-control" placeholder="e.g. HR Management Portal" />
                        </div>

                        <div class="form-group">
                            <label class="form-label" for="txtBaseUrl">Base URL</label>
                            <asp:TextBox ID="txtBaseUrl" runat="server" CssClass="form-control" placeholder="e.g. http://192.168.1.50/hrms/" />
                        </div>

                        <div class="form-group">
                            <label class="form-label" for="txtCallbackUrl">Default Callback URL</label>
                            <asp:TextBox ID="txtCallbackUrl" runat="server" CssClass="form-control" placeholder="e.g. http://192.168.1.50/hrms/Callback.aspx" />
                        </div>

                        <div class="form-group">
                            <label class="form-label" for="txtSecretKey">Shared Secret Key (HMAC)</label>
                            <div style="display: flex; gap: 8px;">
                                <asp:TextBox ID="txtSecretKey" runat="server" CssClass="form-control" placeholder="Used by app to verify HMAC" />
                                <asp:Button ID="btnGenerateKey" runat="server" Text="Generate" CssClass="btn btn-secondary btn-sm" OnClick="btnGenerateKey_Click" />
                            </div>
                        </div>

                        <div class="form-group">
                            <label class="form-label" for="ddlPolicy">Enforced MFA Policy</label>
                            <asp:DropDownList ID="ddlPolicy" runat="server" CssClass="form-control">
                                <asp:ListItem Text="All Users (Mandatory for Everyone)" Value="ALL_USERS" Selected="True" />
                                <asp:ListItem Text="Specific Users Only (Whitelist Mapping)" Value="SPECIFIC_USERS" />
                                <asp:ListItem Text="Enrolled Only (Skip if un-enrolled)" Value="ENROLLED_ONLY" />
                                <asp:ListItem Text="Disabled (Temporary Bypass)" Value="DISABLED" />
                            </asp:DropDownList>
                        </div>
                    </div>

                    <div style="display: flex; align-items: center; justify-content: space-between; margin-top: 12px; padding-top: 12px; border-top: 1px solid var(--gray-200);">
                        <label style="font-size: 0.88rem; font-weight: 600; color: var(--gray-700); display: flex; align-items: center; gap: 8px;">
                            <asp:CheckBox ID="chkIsActive" runat="server" Checked="true" />
                            Application is Active (Allowed to authenticate)
                        </label>

                        <div style="display: flex; gap: 8px;">
                            <asp:Button ID="btnCancelEdit" runat="server" Text="Cancel" CssClass="btn btn-secondary btn-sm" Visible="false" OnClick="btnCancelEdit_Click" />
                            <asp:Button ID="btnSaveApp" runat="server" Text="Save Application" CssClass="btn btn-primary btn-sm" OnClick="btnSaveApp_Click" />
                        </div>
                    </div>
                </div>

                <!-- Existing Applications Table -->
                <div class="data-table-wrapper">
                    <asp:Repeater ID="rptApps" runat="server" OnItemCommand="rptApps_ItemCommand">
                        <HeaderTemplate>
                            <table class="data-table">
                                <thead>
                                    <tr>
                                        <th>App ID</th>
                                        <th>Application Name</th>
                                        <th>Policy Rule</th>
                                        <th>Secret Key (Shared)</th>
                                        <th>Callback URL</th>
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
                                <td><span class="badge badge-info"><%# Eval("PolicyType") %></span></td>
                                <td><code style="background: var(--gray-100); padding: 3px 6px; border-radius: 4px; font-size: 0.78rem;"><%# Eval("SecretKey") %></code></td>
                                <td style="font-size: 0.8rem; color: var(--gray-600);"><%# Eval("CallbackUrl") %></td>
                                <td>
                                    <%# Convert.ToBoolean(Eval("IsActive")) ? "<span class='badge badge-success'>ACTIVE</span>" : "<span class='badge badge-danger'>DISABLED</span>" %>
                                </td>
                                <td>
                                    <div style="display: flex; gap: 6px;">
                                        <asp:LinkButton ID="btnEdit" runat="server" CommandName="EditApp" CommandArgument='<%# Eval("AppId") %>' CssClass="btn btn-secondary btn-sm" Text="Edit" />
                                        <asp:LinkButton ID="btnDel" runat="server" CommandName="DeleteApp" CommandArgument='<%# Eval("AppId") %>' CssClass="btn btn-secondary btn-sm" Text="Delete" OnClientClick="return confirm('Delete this application from registry?');" />
                                    </div>
                                </td>
                            </tr>
                        </ItemTemplate>
                        <FooterTemplate>
                                </tbody>
                            </table>
                        </FooterTemplate>
                    </asp:Repeater>
                </div>
            </div>
        </main>

        <footer class="app-footer">
            <span>CentralMFA Enterprise Gateway &bull; Active Directory + Mantra MFS100 + Oracle 11g</span>
        </footer>
    </form>
</body>
</html>
