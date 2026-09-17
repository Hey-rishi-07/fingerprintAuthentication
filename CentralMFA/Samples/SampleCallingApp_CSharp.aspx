<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="SampleCallingApp_CSharp.aspx.cs" Inherits="CentralMFA.Samples.SampleCallingApp_CSharp" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Calling App Integration Demo (C# ASP.NET) - CentralMFA</title>
    <link href="../Static/app.css" rel="stylesheet" type="text/css" />
</head>
<body style="background: #f1f5f9;">
    <form id="form1" runat="server">
        <header class="app-navbar" style="background: #1e293b;">
            <a href="#" class="brand">
                <span style="font-size: 1.15rem; font-weight: 700; color: #f8fafc;">Enterprise ERP Portal (Calling App Demo)</span>
                <span class="brand-badge" style="background: #334155; color: #94a3b8;">C# ASP.NET</span>
            </a>
            <div style="font-size: 0.82rem; color: #94a3b8;">
                AppId: <strong>ERP</strong>
            </div>
        </header>

        <main class="main-content" style="max-width: 860px; margin: 36px auto; padding: 0 16px;">

            <!-- Status Alert Box -->
            <asp:Panel ID="pnlNotice" runat="server" Visible="false" style="margin-bottom: 24px;">
                <div id="noticeBox" runat="server" class="alert alert-success">
                    <asp:Literal ID="litNotice" runat="server" />
                </div>
            </asp:Panel>

            <!-- Scenario 1: Post-LDAP Login MFA -->
            <div class="card" style="margin-bottom: 24px;">
                <div class="card-header">
                    <h2 class="card-title">Scenario 1: Post-LDAP Login Biometric MFA</h2>
                    <p class="card-desc">
                        Simulates employee entering their Windows Active Directory / LDAP credentials. Once LDAP credentials pass, the employee is redirected to CentralMFA for fingerprint verification.
                    </p>
                </div>

                <div class="form-group">
                    <label class="form-label">Employee PCNO (from LDAP)</label>
                    <asp:TextBox ID="txtLdapPcno" runat="server" CssClass="form-control" Text="EMP001" style="max-width: 320px;" />
                </div>

                <div class="form-group">
                    <label class="form-label">Employee Name</label>
                    <asp:TextBox ID="txtEmployeeName" runat="server" CssClass="form-control" Text="John Doe (Financial Controller)" style="max-width: 320px;" />
                </div>

                <asp:Button ID="btnLoginMFA" runat="server" Text="Simulate LDAP Login &amp; Redirect to MFA Gateway" CssClass="btn btn-primary" OnClick="btnLoginMFA_Click" />
            </div>

            <!-- Scenario 2: Sensitive Button Click / Transaction Authorization -->
            <div class="card">
                <div class="card-header">
                    <h2 class="card-title">Scenario 2: Sensitive Action / Submit Button Authorization</h2>
                    <p class="card-desc">
                        Simulates an authenticated user trying to approve a high-value purchase order or payroll release. Clicking the submit button redirects to CentralMFA with action and transaction ID.
                    </p>
                </div>

                <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin-bottom: 16px;">
                    <div class="form-group">
                        <label class="form-label">Transaction Reference (TxnId)</label>
                        <asp:TextBox ID="txtTxnId" runat="server" CssClass="form-control" Text="TXN-2026-9912" />
                    </div>

                    <div class="form-group">
                        <label class="form-label">Action Identifier</label>
                        <asp:TextBox ID="txtActionType" runat="server" CssClass="form-control" Text="APPROVE_PAYROLL" />
                    </div>
                </div>

                <div class="form-group">
                    <label class="form-label">Approver PCNO</label>
                    <asp:TextBox ID="txtApproverPcno" runat="server" CssClass="form-control" Text="EMP001" style="max-width: 320px;" />
                </div>

                <asp:Button ID="btnAuthorizeTxn" runat="server" Text="Authorize Transaction with Fingerprint" CssClass="btn btn-secondary" OnClick="btnAuthorizeTxn_Click" />
            </div>

        </main>
    </form>
</body>
</html>
