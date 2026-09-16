<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Enroll.aspx.cs" Inherits="Authentication.Enroll" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Fingerprint Enrollment - Oracle 11g Database</title>
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
                <span class="brand-badge">Enrollment</span>
            </a>
            <nav class="nav-links">
                <a href="Login.aspx" class="nav-link">MFA Login</a>
                <a href="Enroll.aspx" class="nav-link active">Fingerprint Enrollment</a>
            </nav>
        </header>

        <!-- Main Content -->
        <main class="main-content">
            <div class="content-wide">
                <div class="auth-card card-wide" style="margin: 0 auto;">
                    <div class="card-header">
                        <h1 class="card-title">Fingerprint Enrollment & Registration</h1>
                        <p class="card-subtitle">Capture fingerprint templates from Mantra MFS100 and store directly as BLOB in Oracle 11g</p>
                    </div>

                    <!-- Demo Mode Toggle & Status -->
                    <div class="demo-toggle">
                        <label class="demo-toggle-label" for="chkDemoMode">Demo Mode (Simulate MFS100 without physical device)</label>
                        <asp:CheckBox ID="chkDemoMode" runat="server" AutoPostBack="true" OnCheckedChanged="chkDemoMode_CheckedChanged" />
                    </div>

                    <!-- Alerts & Notifications -->
                    <asp:Panel ID="pnlStatus" runat="server" Visible="false">
                        <div id="statusBox" runat="server" class="alert alert-info">
                            <asp:Literal ID="litStatusMessage" runat="server" />
                        </div>
                    </asp:Panel>

                    <!-- Enrollment Form -->
                    <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 24px; margin-bottom: 24px;">
                        <div>
                            <div class="form-group">
                                <label class="form-label" for="txtPcNo">Employee ID / PCNO</label>
                                <asp:TextBox ID="txtPcNo" runat="server" CssClass="form-control" placeholder="e.g. 1001, 1002" />
                                <small style="color: var(--gray-500); font-size: 0.78rem; margin-top: 4px; display: block;">
                                    This ID will be used to match against Active Directory logins.
                                </small>
                            </div>

                            <div style="margin-top: 16px;">
                                <asp:Button ID="btnCapture" runat="server" Text="1. Capture Fingerprint" CssClass="btn btn-primary" OnClick="btnCapture_Click" />
                            </div>

                            <div style="margin-top: 10px;">
                                <asp:Button ID="btnSaveToDb" runat="server" Text="2. Save to Oracle Database" CssClass="btn btn-success" Enabled="false" OnClick="btnSaveToDb_Click" />
                            </div>

                            <div style="margin-top: 10px;">
                                <asp:Button ID="btnTestVerify" runat="server" Text="Test Verify with Scanner" CssClass="btn btn-secondary btn-sm" OnClick="btnTestVerify_Click" />
                            </div>
                        </div>

                        <!-- Scanner Visual & Status Feedback -->
                        <div>
                            <div class="scan-container">
                                <div class="scan-icon-wrapper">
                                    <svg class="scan-icon" viewBox="0 0 24 24" fill="currentColor">
                                        <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z"/>
                                    </svg>
                                </div>
                                <h4 style="font-size: 0.95rem; font-weight: 700; color: var(--gray-800); margin-bottom: 4px;">Scanner & Template Status</h4>
                                <p id="lblDeviceStatus" runat="server" style="font-size: 0.82rem; color: var(--primary); font-weight: 600;">Device: Ready</p>
                                
                                <asp:Panel ID="pnlCaptureMeta" runat="server" Visible="false" style="margin-top: 12px; text-align: left; background: #ffffff; padding: 12px; border-radius: 6px; border: 1px solid var(--gray-200);">
                                    <div style="font-size: 0.82rem; color: var(--gray-700); margin-bottom: 4px;">
                                        Template Size: <strong><asp:Literal ID="litTemplateSize" runat="server">0</asp:Literal> bytes</strong>
                                    </div>
                                    <div style="font-size: 0.82rem; color: var(--gray-700); margin-bottom: 6px;">
                                        Quality Score: <strong><asp:Literal ID="litQuality" runat="server">0</asp:Literal> / 100</strong>
                                    </div>
                                    <div class="quality-meter">
                                        <div id="barQuality" runat="server" class="quality-fill" style="width: 0%;"></div>
                                    </div>
                                </asp:Panel>
                            </div>
                        </div>
                    </div>

                    <hr style="border: 0; border-top: 1px solid var(--gray-200); margin: 24px 0;" />

                    <!-- Enrolled Users in Oracle 11g -->
                    <div style="display: flex; align-items: center; justify-content: space-between; margin-bottom: 12px;">
                        <h3 style="font-size: 1.1rem; font-weight: 700; color: var(--gray-800);">Enrolled Users in Oracle 11g</h3>
                        <asp:Button ID="btnRefreshList" runat="server" Text="Refresh List" CssClass="btn btn-secondary btn-sm" OnClick="btnRefreshList_Click" />
                    </div>

                    <div class="data-table-wrapper">
                        <asp:Repeater ID="rptEnrolledUsers" runat="server" OnItemCommand="rptEnrolledUsers_ItemCommand">
                            <HeaderTemplate>
                                <table class="data-table">
                                    <thead>
                                        <tr>
                                            <th>PCNO</th>
                                            <th>Template Size</th>
                                            <th>Quality</th>
                                            <th>Enrolled / Updated</th>
                                            <th>Actions</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                            </HeaderTemplate>
                            <ItemTemplate>
                                <tr>
                                    <td><strong><%# Eval("PcNo") %></strong></td>
                                    <td><%# Eval("TemplateLength") %> bytes</td>
                                    <td><span class="badge badge-success"><%# Eval("Quality") %>/100</span></td>
                                    <td><%# Eval("UpdatedOn", "{0:yyyy-MM-dd HH:mm:ss}") %></td>
                                    <td>
                                        <asp:LinkButton ID="btnDelete" runat="server" CommandName="DeleteUser" CommandArgument='<%# Eval("PcNo") %>' 
                                            CssClass="btn btn-secondary btn-sm" OnClientClick="return confirm('Delete fingerprint for this PCNO?');" Text="Delete" />
                                    </td>
                                </tr>
                            </ItemTemplate>
                            <FooterTemplate>
                                    </tbody>
                                </table>
                            </FooterTemplate>
                        </asp:Repeater>

                        <asp:Panel ID="pnlNoUsers" runat="server" Visible="false" style="padding: 24px; text-align: center; color: var(--gray-500); font-size: 0.88rem;">
                            No fingerprints enrolled in Oracle database yet. Enter a PCNO above to register one.
                        </asp:Panel>
                    </div>
                </div>
            </div>
        </main>

        <!-- Footer -->
        <footer class="app-footer">
            <span>MFA Authentication Portal • Active Directory + Mantra MFS100 + Oracle 11g • 100% Intranet Offline</span>
        </footer>
    </form>
</body>
</html>
