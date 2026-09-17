<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Gateway.aspx.cs" Inherits="CentralMFA.Gateway" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Biometric Verification Gateway - CentralMFA</title>
    <link href="Static/app.css" rel="stylesheet" type="text/css" />
    <script src="Static/mfa-client.js" type="text/javascript"></script>
</head>
<body>
    <form id="form1" runat="server">
        <!-- Hidden fields for captured biometric data -->
        <asp:HiddenField ID="hfCapturedTemplate" runat="server" />
        <asp:HiddenField ID="hfQuality" runat="server" />
        <asp:HiddenField ID="hfIsDemo" runat="server" Value="0" />

        <!-- Header -->
        <header class="app-navbar">
            <a href="Gateway.aspx" class="brand">
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#2563eb" stroke-width="2">
                    <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
                    <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
                </svg>
                <span>CentralMFA</span>
                <span class="brand-badge">Biometric Gateway</span>
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
                <a href="Enroll.aspx" class="nav-link">Fingerprint Enrollment</a>
                <a href="Admin/Dashboard.aspx" class="nav-link">Admin Portal</a>
            </nav>
        </header>

        <!-- Main Content -->
        <main class="main-content">
            <div class="auth-card">
                <div class="card-header">
                    <h1 class="card-title"><asp:Literal ID="litAppTitle" runat="server">Intranet Application</asp:Literal></h1>
                    <p class="card-subtitle"><asp:Literal ID="litSubtitle" runat="server">Biometric Verification Required</asp:Literal></p>
                </div>

                <!-- Action Authorization Banner (if button submit / transaction authorization) -->
                <asp:Panel ID="pnlActionBanner" runat="server" Visible="false" CssClass="action-banner action-highlight">
                    <div class="action-label">Sensitive Transaction Authorization</div>
                    <div class="action-title"><asp:Literal ID="litActionName" runat="server">APPROVE_TRANSACTION</asp:Literal></div>
                    <div class="action-ref">Transaction Reference: <strong><asp:Literal ID="litTxnId" runat="server">TXN-0000</asp:Literal></strong></div>
                </asp:Panel>

                <!-- User Context Banner -->
                <div class="action-banner">
                    <div style="display: flex; justify-content: space-between; align-items: center;">
                        <span style="font-size: 0.84rem; color: var(--gray-600);">Employee PCNO / ID:</span>
                        <strong style="color: var(--gray-900); font-size: 0.96rem;"><asp:Literal ID="litPcNo" runat="server">1001</asp:Literal></strong>
                    </div>
                </div>

                <!-- Status Alerts -->
                <asp:Panel ID="pnlStatus" runat="server" Visible="false">
                    <div id="statusBox" runat="server" class="alert alert-danger">
                        <asp:Literal ID="litStatusMessage" runat="server" />
                    </div>
                </asp:Panel>

                <!-- High-Tech Biometric Sensor Stage -->
                <div class="biometric-stage">
                    <!-- Live Scanner Status Pill -->
                    <div id="scannerPill" class="scanner-status-pill">
                        <span id="scannerLed" class="status-led led-amber"></span>
                        <span id="lblScannerStatus">Detecting MFS100 Scanner on Port 8034...</span>
                    </div>

                    <!-- Sensor Viewport with Futuristic HUD Reticle, Animated Holographic Laser Curtain & Minutiae Nodes -->
                    <div id="scanVisual" class="biometric-viewport" onclick="handleScanAction();" title="Keep finger on scanner and click to scan">
                        <!-- Viewfinder Reticle & HUD Target Brackets -->
                        <div class="hud-corner hud-tl"></div>
                        <div class="hud-corner hud-tr"></div>
                        <div class="hud-corner hud-bl"></div>
                        <div class="hud-corner hud-br"></div>
                        <div class="reticle-ring"></div>
                        <div class="sonar-wave"></div>

                        <!-- Holographic Laser Curtain Sweep -->
                        <div class="laser-beam">
                            <div class="laser-line"></div>
                            <div class="laser-curtain"></div>
                        </div>

                        <!-- Authentic Biometric Fingerprint Ridges & Minutiae Feature Nodes -->
                        <svg class="fingerprint-svg" viewBox="0 0 100 130">
                            <!-- Outermost boundary ridges -->
                            <path d="M50 15 C30 15 16 28 14 48 C12 68 18 90 28 108 C30 112 34 118 36 122" />
                            <path d="M50 15 C70 15 84 28 86 48 C88 68 82 90 72 108 C70 112 66 118 64 122" />
                            <!-- Intermediate whorl ridges -->
                            <path d="M50 25 C35 25 24 36 22 52 C20 68 25 86 34 100 C37 105 40 110 44 116" />
                            <path d="M50 25 C65 25 76 36 78 52 C80 68 75 86 66 100 C63 105 60 110 56 116" />
                            <!-- Core arches -->
                            <path d="M50 35 C40 35 32 44 30 56 C28 68 32 82 40 94 C43 98 47 104 50 108" />
                            <path d="M50 35 C60 35 68 44 70 56 C72 68 68 82 60 94 C57 98 53 104 50 108" />
                            <!-- Inner loop -->
                            <path d="M50 45 C44 45 38 51 38 60 C38 72 43 82 50 88 C57 82 62 72 62 60 C62 51 56 45 50 45 Z" />
                            <!-- Center whorl delta -->
                            <path d="M50 55 C47 55 45 58 45 62 C45 68 48 72 50 74 C52 72 55 68 55 62 C55 58 53 55 50 55 Z" />
                            <!-- Lower delta split ridges -->
                            <path d="M22 80 C26 86 32 94 38 100" />
                            <path d="M78 80 C74 86 68 94 62 100" />
                            <path d="M30 112 C34 117 38 122 42 126" />
                            <path d="M70 112 C66 117 62 122 58 126" />

                            <!-- Minutiae Feature Nodes (Points of interest highlighted dynamically during scan) -->
                            <circle class="minutiae-node" cx="50" cy="55" r="2.8" />
                            <circle class="minutiae-node" cx="38" cy="60" r="2.4" />
                            <circle class="minutiae-node" cx="62" cy="60" r="2.4" />
                            <circle class="minutiae-node" cx="30" cy="56" r="2.2" />
                            <circle class="minutiae-node" cx="70" cy="56" r="2.2" />
                            <circle class="minutiae-node" cx="50" cy="88" r="2.5" />
                        </svg>

                        <!-- Success Badge Overlay with Animated Spring Checkmark -->
                        <div class="scan-success-badge">
                            <svg width="68" height="68" viewBox="0 0 48 48" fill="none">
                                <circle cx="24" cy="24" r="21" fill="#10b981" fill-opacity="0.2" stroke="#10b981" stroke-width="2.5"/>
                                <circle cx="24" cy="24" r="15" fill="#10b981"/>
                                <polyline points="18 24 22 28 30 20" stroke="#ffffff" stroke-width="2.8" stroke-linecap="round" stroke-linejoin="round"/>
                            </svg>
                        </div>
                    </div>

                    <!-- Guidance Headers -->
                    <h3 id="scanHeader" style="font-size: 1.15rem; font-weight: 700; margin-bottom: 4px; color: #f8fafc;">
                        Keep Finger on Scanner
                    </h3>
                    <p id="scanSubheader" style="font-size: 0.84rem; color: #94a3b8; margin: 0 0 8px; line-height: 1.45;">
                        Place your enrolled finger flat and firmly on the optical sensor.
                    </p>

                    <!-- Live Quality Gauge -->
                    <div id="qualityGauge" class="quality-gauge">
                        <div style="display: flex; justify-content: space-between; font-size: 0.76rem; color: #94a3b8; margin-bottom: 4px;">
                            <span>Capture Quality</span>
                            <span id="lblQualityScore" style="color: #38bdf8; font-weight: 700;">85% - Optimal</span>
                        </div>
                        <div class="quality-track">
                            <div id="qualityFill" class="quality-fill" style="width: 0%;"></div>
                        </div>
                    </div>
                </div>

                <!-- Primary Actions -->
                <div class="btn-group" style="flex-direction: column;">
                    <!-- Big Prominent Scan Button -->
                    <button type="button" id="btnScanTrigger" class="btn-scan-main" onclick="handleScanAction();">
                        <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
                            <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2z"></path>
                            <path d="M12 6v6l4 2"></path>
                        </svg>
                        <span id="btnScanLabel">Scan Fingerprint</span>
                    </button>

                    <!-- Auto-Scan on Load Setting -->
                    <div class="auto-scan-row">
                        <label style="display: inline-flex; align-items: center; gap: 6px; cursor: pointer; user-select: none;">
                            <input type="checkbox" id="chkAutoScan" checked="checked" onchange="toggleAutoScan();" style="cursor: pointer; width: 15px; height: 15px;" />
                            <span>Auto-scan on page load</span>
                        </label>
                        <span id="lblAutoCountdown" style="color: #2563eb; font-weight: 700;"></span>
                    </div>

                    <!-- Secondary Options -->
                    <div style="display: flex; gap: 8px; margin-top: 14px;">
                        <button type="button" id="btnDemoTrigger" class="btn btn-secondary btn-sm" style="flex: 1;" onclick="runSimulatedScan();">
                            Simulate Scan (Demo)
                        </button>
                        <asp:Button ID="btnCancel" runat="server" Text="Cancel & Return" CssClass="btn btn-secondary btn-sm" style="flex: 1;" OnClick="btnCancel_Click" />
                    </div>

                    <!-- Client Agent Download Prompt -->
                    <div style="margin-top: 16px; text-align: center; border-top: 1px dashed rgba(255,255,255,0.15); padding-top: 12px;">
                        <span style="font-size: 0.8rem; color: #94a3b8;">Scanner agent not installed? </span>
                        <a href="DownloadAgent.ashx" style="display: inline-flex; align-items: center; gap: 5px; font-size: 0.82rem; color: #38bdf8; font-weight: 600; text-decoration: underline;" title="Download MFS100 Agent ZIP for this PC">
                            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
                                <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
                                <polyline points="7 10 12 15 17 10"></polyline>
                                <line x1="12" y1="15" x2="12" y2="3"></line>
                            </svg>
                            Download Scanner Agent (ZIP)
                        </a>
                    </div>
                </div>

                <!-- Hidden Server Button triggered by JavaScript after capture -->
                <asp:Button ID="btnServerVerify" runat="server" style="display: none;" OnClick="btnServerVerify_Click" />
            </div>
        </main>

        <footer class="app-footer">
            <span>CentralMFA Enterprise Gateway &bull; Active Directory + Mantra MFS100 + Oracle 11g</span>
        </footer>

        <script type="text/javascript">
            let isCapturing = false;
            let autoScanTimer = null;
            let autoCountdownInterval = null;
            let scannerIsOnline = false;
            let deviceIsConnected = false;
            let checkPollTimer = null;

            window.onload = function () {
                initScannerCheck();
            };

            function initScannerCheck() {
                const led = document.getElementById("scannerLed");
                const pillText = document.getElementById("lblScannerStatus");
                const sub = document.getElementById("scanSubheader");

                MfaClient.checkStatus(function (online, data) {
                    scannerIsOnline = online;
                    deviceIsConnected = (online && data && data.Connected === true);

                    if (online && deviceIsConnected) {
                        // STATE 1: Physical MFS100 device is connected & ready
                        led.className = "status-led led-green";
                        const sn = (data && data.SerialNo) ? " (S/N: " + data.SerialNo + ")" : "";
                        pillText.innerHTML = "<strong>Mantra MFS100 Connected</strong>" + sn;
                        pillText.style.color = "#10b981";

                        sub.innerHTML = "Place your enrolled finger flat and firmly on the sensor, then click 'Scan Fingerprint'.";

                        // Auto-scan ONLY runs when real physical device is actually connected
                        const chk = document.getElementById("chkAutoScan");
                        if (chk && chk.checked && !isCapturing && !autoCountdownInterval) {
                            startAutoScanCountdown(2.0);
                        }
                    } else if (online && !deviceIsConnected) {
                        // STATE 2: Agent is running on port 8034, but physical USB device is NOT plugged in!
                        clearAutoTimers();
                        led.className = "status-led led-amber";
                        pillText.innerHTML = "<strong>Agent Online</strong> &bull; <span style='color: #ef4444; font-weight: 700;'>MFS100 USB Not Connected</span>";
                        pillText.style.color = "#f59e0b";

                        sub.innerHTML = "<div style='background: rgba(239, 68, 68, 0.12); border: 1px solid rgba(239, 68, 68, 0.4); border-radius: 6px; padding: 10px; margin: 6px 0; text-align: center; color: #fca5a5;'>" +
                            "<div style='font-weight: 700; font-size: 0.9rem; margin-bottom: 3px;'>No Fingerprint Scanner Detected</div>" +
                            "<div style='font-size: 0.8rem; color: #e2e8f0; margin-bottom: 6px;'>Please plug your Mantra MFS100 scanner into a USB port on this computer.</div>" +
                            "<div style='font-size: 0.76rem; color: #94a3b8;'>Auto-detecting... Once plugged in, the scanner will be recognized automatically.</div>" +
                            "</div>";

                        // Poll every 2 seconds to automatically detect when user plugs in USB
                        if (!checkPollTimer) {
                            checkPollTimer = setTimeout(function () {
                                checkPollTimer = null;
                                initScannerCheck();
                            }, 2000);
                        }
                    } else {
                        // STATE 3: Agent is not running on port 8034
                        clearAutoTimers();
                        led.className = "status-led led-red";
                        pillText.innerHTML = "Scanner Agent port 8034 not detected";
                        pillText.style.color = "#ef4444";

                        sub.innerHTML = "<div style='background: rgba(245, 158, 11, 0.12); border: 1px solid rgba(245, 158, 11, 0.35); border-radius: 6px; padding: 10px; margin: 8px 0; text-align: center; color: #fde68a;'>" +
                            "<div style='font-weight: 600; font-size: 0.88rem; margin-bottom: 4px;'>Scanner Agent Not Running on this PC</div>" +
                            "<div style='font-size: 0.78rem; color: #cbd5e1; margin-bottom: 8px;'>Ensure <em>MfaScannerAgent.exe</em> is running, or download the installer:</div>" +
                            "<a href='DownloadAgent.ashx' class='btn btn-primary btn-sm' style='display: inline-flex; align-items: center; justify-content: center; gap: 6px; text-decoration: none; padding: 6px 14px; font-weight: 600; font-size: 0.82rem;'>" +
                            "<svg width='14' height='14' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><path d='M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4'></path><polyline points='7 10 12 15 17 10'></polyline><line x1='12' y1='15' x2='12' y2='3'></line></svg> Download Scanner Agent Setup (ZIP)" +
                            "</a>" +
                            "</div>" +
                            "<span style='font-size: 0.78rem; color: #94a3b8;'>Or click <strong>Simulate Scan (Demo)</strong> below to test verification without hardware.</span>";
                    }
                });
            }

            function startAutoScanCountdown(seconds) {
                clearAutoTimers();

                let remaining = seconds;
                const countEl = document.getElementById("lblAutoCountdown");
                const sub = document.getElementById("scanSubheader");

                sub.innerHTML = "Keep your finger firmly on the sensor. Auto-capturing in <strong>" + remaining.toFixed(1) + "s</strong>...";
                countEl.innerText = "Auto-scan in " + remaining.toFixed(1) + "s";

                autoCountdownInterval = setInterval(function () {
                    remaining -= 0.5;
                    if (remaining > 0) {
                        sub.innerHTML = "Keep your finger firmly on the sensor. Auto-capturing in <strong>" + remaining.toFixed(1) + "s</strong>...";
                        countEl.innerText = "Auto-scan in " + remaining.toFixed(1) + "s";
                    } else {
                        clearAutoTimers();
                        countEl.innerText = "";
                        startCapture();
                    }
                }, 500);
            }

            function clearAutoTimers() {
                if (autoScanTimer) clearTimeout(autoScanTimer);
                if (autoCountdownInterval) clearInterval(autoCountdownInterval);
                autoScanTimer = null;
                autoCountdownInterval = null;
                const countEl = document.getElementById("lblAutoCountdown");
                if (countEl) countEl.innerText = "";
            }

            function toggleAutoScan() {
                const chk = document.getElementById("chkAutoScan");
                if (!chk.checked) {
                    clearAutoTimers();
                    document.getElementById("scanSubheader").innerText = "Place your enrolled finger flat and firmly on the sensor, then click 'Scan Fingerprint'.";
                } else if (scannerIsOnline && deviceIsConnected && !isCapturing) {
                    startAutoScanCountdown(1.5);
                }
            }

            function handleScanAction() {
                clearAutoTimers();

                if (!scannerIsOnline) {
                    alert("Scanner Agent is not running on port 8034.\n\nPlease start MfaScannerAgent.exe on this PC, or download it from the top navigation bar.");
                    return;
                }

                if (!deviceIsConnected) {
                    alert("Mantra MFS100 Scanner is NOT connected to USB!\n\nPlease plug your MFS100 fingerprint scanner into a USB port on this computer.\n\nIf you do not have a physical scanner and want to test the workflow, click the 'Simulate Scan (Demo)' button.");
                    return;
                }

                startCapture();
            }

            function startCapture() {
                if (isCapturing) return;
                isCapturing = true;
                clearAutoTimers();

                const visual = document.getElementById("scanVisual");
                const header = document.getElementById("scanHeader");
                const sub = document.getElementById("scanSubheader");
                const led = document.getElementById("scannerLed");
                const pillText = document.getElementById("lblScannerStatus");
                const btn = document.getElementById("btnScanTrigger");
                const btnLabel = document.getElementById("btnScanLabel");
                const gauge = document.getElementById("qualityGauge");

                visual.className = "biometric-viewport scanning";
                led.className = "status-led led-blue";
                pillText.innerHTML = "Sensor Active &bull; Keep Finger Pressed...";
                pillText.style.color = "#0ea5e9";

                header.innerText = "Scanning... Keep Finger Pressed";
                sub.innerText = "Hold steady. Communicating with Mantra MFS100 sensor...";
                btn.disabled = true;
                btnLabel.innerText = "Capturing from Sensor...";

                MfaClient.captureFingerprint({ quality: 60, timeout: 10 },
                    function (templateBase64, quality, bmp) {
                        visual.className = "biometric-viewport success";
                        led.className = "status-led led-green";
                        pillText.innerText = "Capture Successful (Quality: " + quality + "%)";
                        pillText.style.color = "#10b981";

                        header.innerText = "Fingerprint Captured!";
                        sub.innerText = "Verifying biometric template against Oracle 11g database...";
                        btnLabel.innerText = "Verifying...";

                        // Show quality gauge bar
                        gauge.style.display = "block";
                        const fill = document.getElementById("qualityFill");
                        const qScore = document.getElementById("lblQualityScore");
                        fill.style.width = Math.min(100, Math.max(10, quality)) + "%";
                        qScore.innerText = quality + "% - " + (quality >= 70 ? "Optimal" : "Acceptable");

                        // Put into hidden fields and submit to server
                        document.getElementById("<%= hfCapturedTemplate.ClientID %>").value = templateBase64;
                        document.getElementById("<%= hfQuality.ClientID %>").value = quality;
                        document.getElementById("<%= hfIsDemo.ClientID %>").value = "0";

                        setTimeout(function () {
                            document.getElementById("<%= btnServerVerify.ClientID %>").click();
                        }, 400);
                    },
                    function (errMsg) {
                        visual.className = "biometric-viewport error";
                        led.className = "status-led led-amber";
                        pillText.innerText = "Capture Incomplete";
                        pillText.style.color = "#ef4444";

                        btn.disabled = false;
                        isCapturing = false;
                        btnLabel.innerText = "Scan Fingerprint";

                        header.innerText = "Fingerprint Not Captured";
                        sub.innerHTML = "<div style='color:#ef4444; font-weight:600; margin: 4px 0;'>" + errMsg + "</div>" +
                                        "<small style='color:#94a3b8;'>Ensure scanner is firmly connected to USB and finger is placed flat on the sensor glass.</small>";
                    }
                );
            }

            function runSimulatedScan() {
                clearAutoTimers();
                const visual = document.getElementById("scanVisual");
                const header = document.getElementById("scanHeader");
                const sub = document.getElementById("scanSubheader");

                visual.className = "biometric-viewport success";
                header.innerText = "Simulated Biometric Match";
                sub.innerText = "Authenticating demo template...";

                document.getElementById("<%= hfIsDemo.ClientID %>").value = "1";
                document.getElementById("<%= hfQuality.ClientID %>").value = "88";
                document.getElementById("<%= hfCapturedTemplate.ClientID %>").value = "SIMULATED_DEMO_TEMPLATE";

                setTimeout(function () {
                    document.getElementById("<%= btnServerVerify.ClientID %>").click();
                }, 300);
            }
        </script>
    </form>
</body>
</html>
