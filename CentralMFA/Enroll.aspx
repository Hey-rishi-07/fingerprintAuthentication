<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Enroll.aspx.cs" Inherits="CentralMFA.Enroll" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Fingerprint Registration - CentralMFA</title>
    <link href="Static/app.css" rel="stylesheet" type="text/css" />
    <script src="Static/mfa-client.js" type="text/javascript"></script>
</head>
<body>
    <form id="form1" runat="server">
        <asp:HiddenField ID="hfEnrollTemplate" runat="server" />
        <asp:HiddenField ID="hfEnrollQuality" runat="server" />

        <header class="app-navbar">
            <a href="Gateway.aspx" class="brand">
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#2563eb" stroke-width="2">
                    <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
                    <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
                </svg>
                <span>CentralMFA</span>
                <span class="brand-badge">Enrollment</span>
            </a>
            <nav class="nav-links">
                <a href="DownloadAgent.ashx" class="nav-link" style="color: #2563eb; font-weight: 600;" title="Download MFS100 Scanner Setup for this PC">
                    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="vertical-align: -2px; margin-right: 3px;">
                        <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
                        <polyline points="7 10 12 15 17 10"></polyline>
                        <line x1="12" y1="15" x2="12" y2="3"></line>
                    </svg>
                    Download Scanner Agent
                </a>
                <a href="Gateway.aspx" class="nav-link">Gateway Test</a>
                <a href="Enroll.aspx" class="nav-link active">Fingerprint Enrollment</a>
                <a href="Admin/Dashboard.aspx" class="nav-link">Admin Portal</a>
            </nav>
        </header>

        <main class="main-content">
            <div class="content-wide">
                <div class="auth-card card-wide" style="margin: 0 auto;">
                    <div class="card-header">
                        <h1 class="card-title">Fingerprint Biometric Enrollment</h1>
                        <p class="card-subtitle">Register user fingerprint templates in Oracle 11g for Multi-Factor Authentication</p>
                    </div>

                    <asp:Panel ID="pnlStatus" runat="server" Visible="false">
                        <div id="statusBox" runat="server" class="alert alert-info">
                            <asp:Literal ID="litStatusMessage" runat="server" />
                        </div>
                    </asp:Panel>

                    <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 24px; margin-bottom: 24px;">
                        <div>
                            <div class="form-group">
                                <label class="form-label" for="txtPcNo">Employee ID / PCNO</label>
                                <asp:TextBox ID="txtPcNo" runat="server" CssClass="form-control" placeholder="e.g. 1001, 1002" />
                                <small style="color: var(--gray-500); font-size: 0.78rem; margin-top: 4px; display: block;">
                                    Matches the LDAP SAMAccountName or EmployeeID attribute.
                                </small>
                            </div>

                            <div style="margin-top: 16px;">
                                <button type="button" class="btn btn-primary" onclick="startEnrollCapture();">
                                    1. Capture Fingerprint from Scanner
                                </button>
                            </div>

                            <div style="margin-top: 10px;">
                                <button type="button" class="btn btn-secondary btn-sm" onclick="startSimulatedEnroll();">
                                    Simulate Capture (Demo Test)
                                </button>
                            </div>

                            <div style="margin-top: 12px;">
                                <asp:Button ID="btnSaveToDb" runat="server" Text="2. Save to Oracle Database" CssClass="btn btn-success" Enabled="false" OnClick="btnSaveToDb_Click" />
                            </div>

                            <div style="margin-top: 14px; font-size: 0.8rem; color: var(--gray-500); display: flex; align-items: center; gap: 4px;">
                                Missing the local scanner software? 
                                <a href="DownloadAgent.ashx" style="color: var(--primary); font-weight: 600; text-decoration: underline; display: inline-flex; align-items: center; gap: 3px;">
                                    <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
                                    Download Setup ZIP
                                </a>
                            </div>
                        </div>

                        <div>
                            <div class="biometric-stage" style="padding: 20px;">
                                <div id="scannerPillEnroll" class="scanner-status-pill" style="margin-bottom: 8px;">
                                    <span id="scannerLedEnroll" class="status-led led-amber"></span>
                                    <span id="lblScannerAgentStatus">Checking port 8034...</span>
                                </div>

                                <div id="scanVisual" class="biometric-viewport" onclick="startEnrollCapture();" style="width: 140px; height: 154px; margin: 0 auto 12px;" title="Keep finger on scanner and click to capture">
                                    <!-- Viewfinder Reticle & HUD Target Brackets -->
                                    <div class="hud-corner hud-tl"></div>
                                    <div class="hud-corner hud-tr"></div>
                                    <div class="hud-corner hud-bl"></div>
                                    <div class="hud-corner hud-br"></div>
                                    <div class="reticle-ring" style="width: 120px; height: 120px;"></div>
                                    <div class="sonar-wave"></div>

                                    <!-- Holographic Laser Curtain Sweep -->
                                    <div class="laser-beam">
                                        <div class="laser-line"></div>
                                        <div class="laser-curtain"></div>
                                    </div>

                                    <!-- Authentic Biometric Ridges & Minutiae Nodes -->
                                    <svg class="fingerprint-svg" viewBox="0 0 100 130" style="width: 86px; height: 110px;">
                                        <path d="M50 15 C30 15 16 28 14 48 C12 68 18 90 28 108 C30 112 34 118 36 122" />
                                        <path d="M50 15 C70 15 84 28 86 48 C88 68 82 90 72 108 C70 112 66 118 64 122" />
                                        <path d="M50 25 C35 25 24 36 22 52 C20 68 25 86 34 100 C37 105 40 110 44 116" />
                                        <path d="M50 25 C65 25 76 36 78 52 C80 68 75 86 66 100 C63 105 60 110 56 116" />
                                        <path d="M50 35 C40 35 32 44 30 56 C28 68 32 82 40 94 C43 98 47 104 50 108" />
                                        <path d="M50 35 C60 35 68 44 70 56 C72 68 68 82 60 94 C57 98 53 104 50 108" />
                                        <path d="M50 45 C44 45 38 51 38 60 C38 72 43 82 50 88 C57 82 62 72 62 60 C62 51 56 45 50 45 Z" />
                                        <path d="M50 55 C47 55 45 58 45 62 C45 68 48 72 50 74 C52 72 55 68 55 62 C55 58 53 55 50 55 Z" />
                                        <path d="M22 80 C26 86 32 94 38 100" />
                                        <path d="M78 80 C74 86 68 94 62 100" />

                                        <!-- Minutiae Feature Nodes -->
                                        <circle class="minutiae-node" cx="50" cy="55" r="2.8" />
                                        <circle class="minutiae-node" cx="38" cy="60" r="2.4" />
                                        <circle class="minutiae-node" cx="62" cy="60" r="2.4" />
                                        <circle class="minutiae-node" cx="50" cy="88" r="2.5" />
                                    </svg>

                                    <!-- Success Badge Overlay with Spring Checkmark -->
                                    <div class="scan-success-badge">
                                        <svg width="60" height="60" viewBox="0 0 48 48" fill="none">
                                            <circle cx="24" cy="24" r="21" fill="#10b981" fill-opacity="0.2" stroke="#10b981" stroke-width="2.5"/>
                                            <circle cx="24" cy="24" r="15" fill="#10b981"/>
                                            <polyline points="18 24 22 28 30 20" stroke="#ffffff" stroke-width="2.8" stroke-linecap="round" stroke-linejoin="round"/>
                                        </svg>
                                    </div>
                                </div>

                                <div id="pnlCaptureFeedback" style="display: none; margin-top: 10px; text-align: left; background: #1e293b; padding: 10px 14px; border-radius: 6px; border: 1px solid #334155;">
                                    <div style="display: flex; justify-content: space-between; font-size: 0.8rem; color: #94a3b8; margin-bottom: 4px;">
                                        <span>Template Size: <strong id="lblBytesCount" style="color: #f8fafc;">0</strong> bytes</span>
                                        <span>Quality: <strong id="lblQualityScore" style="color: #10b981;">0</strong> / 100</span>
                                    </div>
                                    <div class="quality-track">
                                        <div id="barQualityFill" class="quality-fill" style="width: 0%;"></div>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>

                    <hr style="border: 0; border-top: 1px solid var(--gray-200); margin: 24px 0;" />

                    <!-- Enrolled Users in Oracle 11g -->
                    <div style="display: flex; align-items: center; justify-content: space-between; margin-bottom: 12px;">
                        <h3 style="font-size: 1.1rem; font-weight: 700; color: var(--gray-800);">Registered Biometric Users in Oracle 11g</h3>
                        <asp:Button ID="btnRefresh" runat="server" Text="Refresh List" CssClass="btn btn-secondary btn-sm" OnClick="btnRefresh_Click" />
                    </div>

                    <div class="data-table-wrapper">
                        <asp:Repeater ID="rptUsers" runat="server" OnItemCommand="rptUsers_ItemCommand">
                            <HeaderTemplate>
                                <table class="data-table">
                                    <thead>
                                        <tr>
                                            <th>PCNO / Employee ID</th>
                                            <th>Template Size</th>
                                            <th>Quality</th>
                                            <th>Last Enrolled / Updated</th>
                                            <th>Action</th>
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
                                        <asp:LinkButton ID="btnDel" runat="server" CommandName="DeleteUser" CommandArgument='<%# Eval("PcNo") %>'
                                            CssClass="btn btn-secondary btn-sm" Text="Delete" OnClientClick="return confirm('Delete this enrolled fingerprint from Oracle?');" />
                                    </td>
                                </tr>
                            </ItemTemplate>
                            <FooterTemplate>
                                    </tbody>
                                </table>
                            </FooterTemplate>
                        </asp:Repeater>

                        <asp:Panel ID="pnlNoUsers" runat="server" Visible="false" style="padding: 24px; text-align: center; color: var(--gray-500);">
                            No fingerprints enrolled in Oracle 11g yet.
                        </asp:Panel>
                    </div>
                </div>
            </div>
        </main>

        <footer class="app-footer">
            <span>CentralMFA Enterprise Gateway &bull; Active Directory + Mantra MFS100 + Oracle 11g</span>
        </footer>

        <script type="text/javascript">
            let enrollDeviceConnected = false;
            let enrollAgentOnline = false;
            let enrollPollTimer = null;

            window.onload = function () {
                checkEnrollScanner();
            };

            function checkEnrollScanner() {
                MfaClient.checkStatus(function (online, data) {
                    enrollAgentOnline = online;
                    enrollDeviceConnected = (online && data && data.Connected === true);

                    const el = document.getElementById("lblScannerAgentStatus");
                    const led = document.getElementById("scannerLedEnroll");

                    if (online && enrollDeviceConnected) {
                        if (led) led.className = "status-led led-green";
                        el.style.color = "var(--success)";
                        const sn = (data && data.SerialNo) ? " (S/N: " + data.SerialNo + ")" : "";
                        el.innerHTML = "<strong>Mantra MFS100 Connected</strong>" + sn;
                    } else if (online && !enrollDeviceConnected) {
                        if (led) led.className = "status-led led-amber";
                        el.style.color = "#d97706";
                        el.innerHTML = "<strong>Agent Online</strong> &bull; <span style='color:#ef4444; font-weight:600;'>MFS100 USB Not Connected</span>";

                        // Auto-poll to detect when device is plugged in
                        if (!enrollPollTimer) {
                            enrollPollTimer = setTimeout(function () {
                                enrollPollTimer = null;
                                checkEnrollScanner();
                            }, 2500);
                        }
                    } else {
                        if (led) led.className = "status-led led-amber";
                        el.style.color = "#d97706";
                        el.innerHTML = "Scanner Agent offline &bull; <a href='DownloadAgent.ashx' style='color: #2563eb; text-decoration: underline; font-weight: 600;'>Download Agent (ZIP)</a>";
                    }
                });
            }

            function startEnrollCapture() {
                const pcno = document.getElementById("<%= txtPcNo.ClientID %>").value.trim();
                if (!pcno) {
                    alert("Please enter an Employee ID / PCNO first.");
                    return;
                }

                if (!enrollAgentOnline) {
                    alert("Scanner Agent is not running on port 8034.\n\nPlease start MfaScannerAgent.exe on this PC, or download the setup package.");
                    return;
                }

                if (!enrollDeviceConnected) {
                    alert("Mantra MFS100 scanner is NOT connected to USB!\n\nPlease plug your MFS100 device into this computer's USB port.\n\nIf you do not have a physical scanner and want to test enrollment, click 'Simulate Capture (Demo Test)'.");
                    return;
                }

                const visual = document.getElementById("scanVisual");
                visual.className = "biometric-viewport scanning";

                MfaClient.captureFingerprint({ quality: 60, timeout: 10 },
                    function (templateBase64, quality, bmp) {
                        visual.className = "biometric-viewport success";
                        showCaptureFeedback(templateBase64, quality);
                    },
                    function (err) {
                        visual.className = "biometric-viewport error";
                        alert("Capture Error: " + err);
                    }
                );
            }

            function startSimulatedEnroll() {
                const pcno = document.getElementById("<%= txtPcNo.ClientID %>").value.trim();
                if (!pcno) {
                    alert("Please enter an Employee ID / PCNO first.");
                    return;
                }
                // Deterministic demo template
                const demoBytes = new Uint8Array(400);
                for (let i = 0; i < 400; i++) demoBytes[i] = (i ^ pcno.charCodeAt(i % pcno.length)) & 0xFF;
                let binary = '';
                for (let i = 0; i < demoBytes.byteLength; i++) binary += String.fromCharCode(demoBytes[i]);
                const base64 = window.btoa(binary);

                const visual = document.getElementById("scanVisual");
                visual.className = "biometric-viewport success";
                showCaptureFeedback(base64, 85);
            }

            function showCaptureFeedback(templateBase64, quality) {
                document.getElementById("<%= hfEnrollTemplate.ClientID %>").value = templateBase64;
                document.getElementById("<%= hfEnrollQuality.ClientID %>").value = quality;

                document.getElementById("pnlCaptureFeedback").style.display = "block";
                document.getElementById("lblBytesCount").innerText = Math.round(templateBase64.length * 0.75);
                document.getElementById("lblQualityScore").innerText = quality;
                document.getElementById("barQualityFill").style.width = quality + "%";

                document.getElementById("<%= btnSaveToDb.ClientID %>").disabled = false;
            }
        </script>
    </form>
</body>
</html>
