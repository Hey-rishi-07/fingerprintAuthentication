# CentralMFA - Comprehensive System & Page-by-Page Architecture Guide

This document provides a detailed, comprehensive explanation of **every web page, component, utility, and integration sample** in the **CentralMFA** system.

---

## Table of Contents

1. [High-Level Architecture & Interaction Flow](#high-level-architecture--interaction-flow)
2. [End-User & Gateway Pages](#end-user--gateway-pages)
   - [1. Gateway.aspx - Central Biometric Verification Gateway](#1-gatewayaspx---central-biometric-verification-gateway)
   - [2. Enroll.aspx - Employee Fingerprint Enrollment](#2-enrollaspx---employee-fingerprint-enrollment)
3. [Administrator Management Portal (`/Admin/`)](#administrator-management-portal-admin)
   - [3. Admin/Login.aspx - Administrator Authentication](#3-adminloginaspx---administrator-authentication)
   - [4. Admin/Dashboard.aspx - Security & Metrics Overview](#4-admindashboardaspx---security--metrics-overview)
   - [5. Admin/Apps.aspx - Registered Applications & Secret Keys](#5-adminappsaspx---registered-applications--secret-keys)
   - [6. Admin/Policies.aspx - Multi-Factor Enforcement Policies](#6-adminpoliciesaspx---multi-factor-enforcement-policies)
   - [7. Admin/UserMapping.aspx - Application User Whitelisting](#7-adminusermappingaspx---application-user-whitelisting)
   - [8. Admin/AuditLogs.aspx - Real-Time Biometric Audit Trail](#8-adminauditlogsaspx---real-time-biometric-audit-trail)
4. [Calling Application Integration Samples (`/Samples/`)](#calling-application-integration-samples-samples)
   - [9. Samples/SampleCallingApp_CSharp.aspx - ASP.NET C# Integration](#9-samplessamplecallingapp_csharpaspx---aspnet-c-integration)
   - [10. Samples/sample_calling_app_php.php - PHP Integration](#10-samplessample_calling_app_phpphp---php-integration)
   - [11. Samples/sample_calling_app_asp.asp - Classic ASP (VBScript) Integration](#11-samplessample_calling_app_aspasp---classic-asp-vbscript-integration)
5. [Client PC Scanner Agent (`/ClientAgent/`)](#client-pc-scanner-agent-clientagent)
   - [12. MfaScannerAgent.exe & install.bat](#12-mfascanneragentexe--installbat)
6. [Core Engine Utilities (`/Utils/`)](#core-engine-utilities-utils)
   - [13. Utils/DBHelper.cs - Oracle 11g Database Engine](#13-utilsdbhelpercs---oracle-11g-database-engine)
   - [14. Utils/DeviceManager.cs - Biometric Matching Engine](#14-utilsdevicemanagercs---biometric-matching-engine)
   - [15. Utils/SecurityHelper.cs - Cryptographic Token & Replay Engine](#15-utilssecurityhelpercs---cryptographic-token--replay-engine)
7. [Database Schema Reference (`Mfa_Schema.sql`)](#database-schema-reference-mfa_schemasql)
8. [Summary of Error Codes & Edge Case Handling](#summary-of-error-codes--edge-case-handling)

---

## High-Level Architecture & Interaction Flow

In a secure offline corporate intranet:
1. **Calling Applications** (ERP, HRMS, Payroll, CRM, Legacy ASP) run on intranet web servers.
2. **Oracle 11g** acts as the central biometric template repository and audit store.
3. **Mantra MFS100 Scanners** are plugged into individual employee PCs via USB.
4. **MfaScannerAgent.exe** runs locally on employee PCs, exposing port `8034` to browser JavaScript.
5. **CentralMFA** acts as the central verification broker and policy enforcement hub.

### The Two Authentication Workflows

#### Workflow A: Post-LDAP Login MFA
```
[Employee] ──(1. Enters AD / LDAP credentials)──► [Calling App Login Page]
                                                          │
                                                (2. LDAP Pass OK)
                                                          ▼
[Employee Browser] ◄──(3. Redirect with AppId & PCNO)─────┘
        │
        ▼
[CentralMFA Gateway.aspx] ──(4. Check Policy & Fetch Template)──► [Oracle 11g]
        │
        ├─► [MfaScannerAgent:8034] ──► Reads USB Scanner ──► Returns Live ISO Template
        │
        ├─► [DeviceManager] ──────────► Matches Live Scan vs Oracle BLOB (Threshold 1400)
        │
        ▼
[Employee Browser] ◄──(5. Signed Redirect with HMAC-SHA256 Token)───┘
        │
        ▼
[Calling App Callback] ──(6. Verifies Signature & Starts User Session)──► [Dashboard]
```

#### Workflow B: Submit-Button / Step-Up Transaction Authorization
```
[Logged-In User] ──(Clicks 'Approve Payroll' or 'Submit Order')──► [Calling App Form]
                                                                        │
                                                (Prepares Action & TxnId)
                                                                        ▼
[Employee Browser] ◄──(Redirect: action=APPROVE_PAYROLL&txnId=TXN-101)──┘
        │
        ▼
[CentralMFA Gateway.aspx] Displays: "Approve Payroll | Ref: TXN-101 | Approver: EMP001"
        │
        ├─► User scans fingerprint on MFS100
        ├─► CentralMFA records audit log in Oracle: "TXN-101 | APPROVED | Score 3850"
        │
        ▼
[Calling App Callback] ──(Validates Signature)──► Executes sensitive SQL transaction!
```

---

## End-User & Gateway Pages

### 1. Gateway.aspx - Central Biometric Verification Gateway

* **Physical Location**: `CentralMFA/Gateway.aspx` & `Gateway.aspx.cs`
* **Target Audience**: All intranet employees redirected from calling applications.
* **Primary URL Parameters**:
  - `appId` *(required)*: Unique identifier of calling application (e.g. `ERP`, `HRMS`, `PAYROLL`).
  - `pcno` *(required)*: Employee workstation or employee ID from LDAP (e.g. `EMP001`).
  - `action` *(optional, default: `LOGIN`)*: Purpose of the request (`LOGIN`, `APPROVE_PAYROLL`, `SUBMIT_PURCHASE`).
  - `txnId` *(optional)*: Specific transaction reference identifier (e.g. `TXN-2026-9912`).
  - `returnUrl` *(optional)*: Callback URL where CentralMFA redirects the user upon completion. If omitted, CentralMFA looks up the registered `CALLBACK_URL` in Oracle.
  - `timestamp` & `nonce` & `sig`: Optional parameters for cryptographic validation of incoming requests.

#### What This Page Does:
1. **Application & Policy Evaluation (`Page_Load`)**:
   - Queries Oracle 11g to verify `appId` exists and is marked `IS_ACTIVE = 1`. If not, renders an error alert.
   - Evaluates the application's configured policy:
     - **`DISABLED`**: Immediately bypasses biometric check, generates an approved HMAC token, and redirects back to the calling app with `status=APPROVED` (Reason: `MFA_POLICY_DISABLED`).
     - **`SPECIFIC_USERS`**: Checks `MFA_APP_USER_WHITELIST` table. If the `pcno` is **not** on the whitelist, the employee is exempt and auto-redirects with `status=APPROVED` (Reason: `EXEMPT_USER_POLICY`).
     - **`ENROLLED_ONLY`**: Checks if the user has an enrolled fingerprint in Oracle. If not enrolled, auto-bypasses with `status=APPROVED` (Reason: `NO_ENROLLED_FINGERPRINT`).
     - **`ALL_USERS`**: Enforces biometric authentication unconditionally.
2. **Context Banners**:
   - If `action != 'LOGIN'`, renders a high-visibility amber/blue banner showing the **Action Identifier** and **Transaction Reference ID**.
   - Displays the Employee PCNO so the user knows exactly whose biometric profile is being verified.
3. **High-Tech Biometric Sensor Pad**:
   - Displays an animated dark sensor pad featuring realistic SVG fingerprint ridges.
   - Houses a **glowing cyan laser scanning beam** that animates up and down during scanning.
   - Features a live **LED Status Pill**:
     - 🟢 **Green**: Mantra MFS100 scanner connected on port 8034.
     - 🔵 **Blue**: Sensor actively capturing fingerprint.
     - 🟡 **Amber**: Scanner agent initializing or finger placed too lightly.
     - 🔴 **Red**: Agent offline or scanner disconnected.
4. **Auto-Scan on Load Feature**:
   - When the page loads, JavaScript contacts `http://127.0.0.1:8034/mfs100/info`.
   - If the scanner is ready and the **"Auto-scan on page load"** setting is enabled, a 1.5-second countdown starts:
     > *"Keep your finger firmly on the sensor. Auto-capturing in 1.5s..."*
   - When the countdown finishes, it triggers the scan automatically. An employee who already has their finger on the sensor gets verified **without clicking a button**!
5. **Manual Touch-and-Scan Workflow**:
   - Employees can keep their finger placed on the glass and click the large primary button: **`[ 👆 Scan Fingerprint ]`**.
   - The button switches to `Capturing from Sensor...`, laser sweeps, and scanner acquires the biometric template.
6. **Live Quality Meter & Postback**:
   - Upon capture, a progress bar displays the capture quality (e.g. `86% - Optimal`).
   - The ISO template is placed into `hfCapturedTemplate` and triggers hidden server button `btnServerVerify`.
7. **Biometric Matching & Signature Generation (`btnServerVerify_Click`)**:
   - Retrieves the enrolled BLOB template from Oracle (`APP_USER_FINGERPRINTS`).
   - Calls `DeviceManager.Instance.MatchFingerprints(storedTemplate, liveTemplate)`.
   - If `matchScore >= 1400` (Mantra ISO match threshold):
     - Logs `APPROVED` into `MFA_AUDIT_LOGS`.
     - Generates HMAC-SHA256 signature using the app's registered secret key.
     - Redirects to `returnUrl` with: `status=APPROVED&appId=...&pcno=...&action=...&txnId=...&score=3850&timestamp=...&nonce=...&sig=...`.
   - If `matchScore < 1400`:
     - Logs `DENIED` into `MFA_AUDIT_LOGS`.
     - Shows error alert on screen: *"Fingerprint Verification Failed! Match score: 420 (Required: 1400). Fingerprint does not match the enrolled template."*
8. **Demo / Simulation Mode**:
   - Features a button **"Test with Simulated Scan (Demo)"** allowing developers/testers to simulate a successful match with score `3850` without physical scanner hardware.

---

### 2. Enroll.aspx - Employee Fingerprint Enrollment

* **Physical Location**: `CentralMFA/Enroll.aspx` & `Enroll.aspx.cs`
* **Target Audience**: HR administrators, IT helpdesk engineers, or employees registering their biometric template.
* **URL Parameters**:
  - `pcno` *(optional)*: Pre-populates the Employee ID / PCNO textbox.

#### What This Page Does:
1. **Employee Identification**:
   - Textbox for Employee ID / PCNO (matches the employee's Windows Active Directory `sAMAccountName` or Oracle employee number).
2. **Scanner Stage**:
   - Features the same high-tech biometric sensor pad, glowing laser beam, and live scanner agent status.
3. **Capture Phase**:
   - User places finger on scanner and clicks **"1. Capture Fingerprint from Scanner"**.
   - Talks to `http://127.0.0.1:8034/mfs100/capture`.
   - Displays real-time template size (e.g. `384 bytes`) and quality rating (e.g. `85 / 100`) on a progress bar.
   - Alternatively, clicking **"Simulate Capture (Demo Test)"** generates a valid synthetic ISO template for instant testing.
4. **Database Persistence**:
   - Unlocks the button **"2. Save to Oracle Database"**.
   - Clicking it executes `DBHelper.SaveFingerprint(pcno, templateBytes, quality)`.
   - Performs an atomic `MERGE INTO APP_USER_FINGERPRINTS` query in Oracle 11g, storing the template as a raw `BLOB`.
5. **Enrolled Users Registry (Lower Section)**:
   - Queries Oracle and displays a searchable table of all enrolled employees.
   - Shows: PCNO, Template Length, Quality Score, Last Enrolled / Updated Date.
   - Provides a **Delete** button with confirmation popup to remove obsolete templates from Oracle.

---

## Administrator Management Portal (`/Admin/`)

All administration pages reside in the `/Admin/` folder and require active administrative authentication.

### 3. Admin/Login.aspx - Administrator Authentication

* **Physical Location**: `CentralMFA/Admin/Login.aspx` & `Admin/Login.aspx.cs`
* **Target Audience**: System Administrators & IT Security Officers.

#### What This Page Does:
1. **Credentials Validation**:
   - Accepts **Username** and **Password**.
   - Validates against configured credentials in [Web.config](file:///e:/fingerprint_scanner/CentralMFA/Web.config):
     - Default Username: `admin`
     - Default Password: `admin123`
     *(Configurable via `<appSettings><add key="AdminUsername" ... /><add key="AdminPassword" ... /></appSettings>`)*.
2. **Session Security**:
   - Sets `Session["IsAdminLoggedIn"] = true` and `Session["AdminUser"] = username`.
   - Redirects to `Admin/Dashboard.aspx`.
   - If credentials fail, displays a red alert box: *"Invalid administrator username or password."*

---

### 4. Admin/Dashboard.aspx - Security & Metrics Overview

* **Physical Location**: `CentralMFA/Admin/Dashboard.aspx` & `Admin/Dashboard.aspx.cs`
* **Target Audience**: Administrators monitoring system health.

#### What This Page Does:
1. **Real-Time KPI Metric Cards**:
   - **Registered Apps**: Total intranet applications configured in the gateway.
   - **Active Apps**: Applications currently enforcing MFA.
   - **Enrolled Employees**: Count of unique employee fingerprint BLOB templates stored in Oracle.
   - **Today's Authentications**: Total biometric verification attempts since midnight.
   - **Approved Today**: Successful biometric authentications.
   - **Denied Today**: Failed/mismatched authentications.
2. **Applications Summary Table**:
   - Lists all registered applications, their configured enforcement policy, callback URLs, and active/inactive toggle badges.
3. **Recent Activity Feed**:
   - Displays the 5 most recent biometric verification attempts across the company with status badges (`APPROVED` / `DENIED`), PCNOs, match scores, and timestamps.

---

### 5. Admin/Apps.aspx - Registered Applications & Secret Keys

* **Physical Location**: `CentralMFA/Admin/Apps.aspx` & `Admin/Apps.aspx.cs`
* **Target Audience**: IT administrators onboarding new intranet applications into CentralMFA.

#### What This Page Does:
1. **Application Registration Form**:
   - **Application ID (AppId)**: Unique code (e.g. `HRMS`, `ERP`, `PAYROLL`, `PORTAL`).
   - **Application Display Name**: Human-friendly title shown to employees on the gateway screen.
   - **Base URL**: Root URL of the calling application (e.g. `http://hrms.corp.local/`).
   - **Callback URL**: Return endpoint where CentralMFA posts approval tokens (e.g. `http://hrms.corp.local/Callback.aspx`).
   - **Cryptographic Shared Secret Key**:
     - A 256-bit cryptographic key used for HMAC-SHA256 signing.
     - Includes a button: **"Generate 256-bit Key"**, which generates high-entropy random keys via .NET `RNGCryptoServiceProvider`.
   - **Default Enforcement Policy**: Dropdown to pick initial policy (`ALL_USERS`, `SPECIFIC_USERS`, `ENROLLED_ONLY`, `DISABLED`).
   - **Is Active**: Checkbox to toggle whether MFA is active for this application.
2. **Editing & Deleting Apps**:
   - The table below lists all registered applications with their AppId, Base URL, Callback URL, Secret Key (masked with toggle), Policy, and Active status.
   - Administrators can click **Edit** to populate the form and update settings, or **Delete** (with confirmation) to permanently remove the app from Oracle.

---

### 6. Admin/Policies.aspx - Multi-Factor Enforcement Policies

* **Physical Location**: `CentralMFA/Admin/Policies.aspx` & `Admin/Policies.aspx.cs`
* **Target Audience**: Security officers defining when and for whom biometric MFA is required.

#### What This Page Does:
1. **Application Selector**:
   - Dropdown listing all registered applications.
   - When an application is selected, the page asynchronously loads its current enforcement policy.
2. **Policy Selection Radio Cards**:
   - 🔵 **`ALL_USERS` (Mandatory for Everyone)**:
     *Every* user accessing this application must authenticate via fingerprint. Recommended for high-security applications like Payroll and ERP.
   - 🟡 **`SPECIFIC_USERS` (Restricted User List)**:
     Only employees whose PCNOs are explicitly added to the application whitelist are prompted for fingerprint. All other users bypass automatically.
     *(When selected, displays a direct link to `UserMapping.aspx` to manage the employee whitelist).*
   - 🟢 **`ENROLLED_ONLY` (Gradual Rollout / Grace Period)**:
     Only users who have already enrolled their fingerprint in Oracle are prompted. Unenrolled users bypass automatically. Ideal for initial company rollout so non-enrolled staff are not locked out.
   - ⚪ **`DISABLED` (MFA Bypassed)**:
     Temporarily disables biometric MFA for this app (e.g. during system maintenance or hardware driver upgrades).
3. **Persistence**:
   - Clicking **"Save Policy Configuration"** executes a `MERGE INTO MFA_APP_POLICIES` query in Oracle 11g, taking effect across all intranet workstations immediately.

---

### 7. Admin/UserMapping.aspx - Application User Whitelisting

* **Physical Location**: `CentralMFA/Admin/UserMapping.aspx` & `Admin/UserMapping.aspx.cs`
* **Target Audience**: Administrators managing user access for applications using the `SPECIFIC_USERS` policy.

#### What This Page Does:
1. **Application Selector**:
   - Dropdown to choose which registered application's whitelist is being managed.
2. **Add Whitelist Entry**:
   - Textbox to enter an employee PCNO (e.g. `EMP001`, `FIN_MGR`).
   - Clicking **"Add User to Whitelist"** saves the entry into Oracle table `MFA_APP_USER_WHITELIST`.
3. **Whitelisted Users Table**:
   - Displays all employee PCNOs currently mapped to the selected application, along with the date added.
   - Features a **Remove** button to take employees off the mandatory MFA list.

---

### 8. Admin/AuditLogs.aspx - Real-Time Biometric Audit Trail

* **Physical Location**: `CentralMFA/Admin/AuditLogs.aspx` & `Admin/AuditLogs.aspx.cs`
* **Target Audience**: Security auditors and system administrators reviewing access history.

#### What This Page Does:
1. **Complete Security Trail Table**:
   - Queries `MFA_AUDIT_LOGS` table in Oracle 11g, ordered by `CREATED_ON DESC`.
   - Columns:
     - **Log ID**: Unique sequence number.
     - **Timestamp**: Exact date and time (`YYYY-MM-DD HH:MM:SS`).
     - **Application**: The `AppId` that initiated the request (`ERP`, `HRMS`, `PAYROLL`).
     - **PCNO**: Employee identifier who attempted verification.
     - **Action Type**: `LOGIN`, `APPROVE_PAYROLL`, `SANCTION_LEAVE`, etc.
     - **Txn Ref**: Transaction reference ID (if triggered via submit button).
     - **Match Score**: Biometric algorithm score (e.g. `3850` vs required threshold `1400`).
     - **Status Badge**:
       - 🟢 `APPROVED`: Verification succeeded or approved by policy.
       - 🔴 `DENIED`: Fingerprint mismatch (score below threshold).
       - 🟡 `CANCELLED`: User clicked cancel or timed out.
     - **Client IP**: Workstation IP address of the user.
     - **Details**: Specific audit reason (`BIOMETRIC_MATCH_SUCCESS`, `EXEMPT_USER_POLICY`, etc.).
2. **Live Refresh**:
   - Features a **"Refresh Logs"** button to immediately pull the latest events without reloading the entire portal.

---

## Calling Application Integration Samples (`/Samples/`)

These files serve as **complete, working integration blueprints** demonstrating how any intranet application in C#, PHP, or Classic ASP calls CentralMFA and validates responses.

### 9. Samples/SampleCallingApp_CSharp.aspx - ASP.NET C# Integration

* **Physical Location**: `CentralMFA/Samples/SampleCallingApp_CSharp.aspx` & `SampleCallingApp_CSharp.aspx.cs`
* **Technology**: ASP.NET Web Forms (.NET 4.5 C#)
* **Registered AppId**: `ERP` (Secret: `ERP_SECRET_KEY_77B1C9D2`)

#### What This Page Does:
1. **Scenario 1: Post-LDAP Login MFA**:
   - Employee enters PCNO `EMP001`.
   - Clicking **"Simulate LDAP Login & Redirect to MFA Gateway"** builds the gateway URL with `action=LOGIN`, timestamps, and nonces, then redirects to `Gateway.aspx`.
2. **Scenario 2: Sensitive Submit Button Authorization**:
   - Form specifies Transaction Ref `TXN-2026-9912`, Action `APPROVE_PAYROLL`, Approver `EMP001`.
   - Clicking **"Authorize Transaction with Fingerprint"** redirects to `Gateway.aspx` for step-up biometric sign-off.
3. **Gateway Callback Handling (`Page_Load`)**:
   - When CentralMFA redirects back, reads: `status`, `appId`, `pcno`, `action`, `txnId`, `timestamp`, `nonce`, `sig`.
   - **HMAC Verification**:
     ```csharp
     string payload = SecurityHelper.BuildPayload(appId, pcno, status, action, txnId, timestamp, nonce);
     bool isValid = SecurityHelper.VerifyHmacSha256(payload, sig, APP_SECRET);
     ```
   - **Timestamp Freshness**:
     ```csharp
     bool isFresh = SecurityHelper.IsTimestampValid(timestamp, 120); // Within 2 minutes
     ```
   - If valid and `status == "APPROVED"`, displays a green success alert:
     > *"MFA Login Approved! Biometric verification succeeded for Employee PCNO: EMP001. Proceeding into ERP portal dashboard..."*
   - If signature is tampered or corrupted, displays a red security alert.

---

### 10. Samples/sample_calling_app_php.php - PHP Integration

* **Physical Location**: `CentralMFA/Samples/sample_calling_app_php.php`
* **Technology**: Single-file PHP 5.4 / 7.x / 8.x
* **Registered AppId**: `HRMS` (Secret: `HRMS_SECRET_KEY_8F9A2B1C`)

#### What This Page Does:
1. **Redirect Generation**:
   - Generates GMT timestamp (`gmdate('YmdHis')`) and 16-character random nonce.
   - Signs the request using PHP built-in `hash_hmac('sha256', $payload, APP_SECRET)`.
   - Emits HTTP `302` redirect to CentralMFA Gateway.
2. **Callback Validation**:
   - Reads `$_GET['status']`, `$_GET['sig']`, etc.
   - Computes expected hash:
     ```php
     $dataToVerify = "{$appId}|{$pcno}|{$status}|{$action}|{$txnId}|{$timestamp}|{$nonce}";
     $expectedSig = hash_hmac('sha256', $dataToVerify, APP_SECRET);
     ```
   - Validates using timing-safe comparison:
     ```php
     if (hash_equals(strtolower($expectedSig), strtolower($sig))) { ... }
     ```
   - Checks that `abs(time() - (int)$timestamp) <= 120`.

---

### 11. Samples/sample_calling_app_asp.asp - Classic ASP (VBScript) Integration

* **Physical Location**: `CentralMFA/Samples/sample_calling_app_asp.asp`
* **Technology**: Classic ASP / VBScript (for legacy intranet portals)
* **Registered AppId**: `ERP` (Secret: `ERP_SECRET_KEY_77B1C9D2`)

#### What This Page Does:
1. Demonstrates how legacy Classic ASP applications running on Windows Server can compute HMAC-SHA256 without external COM DLLs by utilizing Windows built-in `System.Security.Cryptography.HMACSHA256`:
   ```vbscript
   Function ComputeHmacSha256(dataStr, secretStr)
       Dim utf8, hmac
       Set utf8 = CreateObject("System.Text.UTF8Encoding")
       Set hmac = CreateObject("System.Security.Cryptography.HMACSHA256")
       hmac.Key = utf8.GetBytes_4(secretStr)
       ComputeHmacSha256 = Hex(hmac.ComputeHash_2(utf8.GetBytes_4(dataStr)))
   End Function
   ```
2. Initiates post-LDAP redirect and submit-button transaction authorization.
3. Verifies returning HMAC signatures and handles approved/denied states.

---

## Client PC Scanner Agent (`/ClientAgent/`)

### 12. MfaScannerAgent.exe & install.bat

* **Physical Location**: `CentralMFA/ClientAgent/`
* **Binary**: `MfaScannerAgent.exe` (.NET Framework 4.5 Console/Daemon)
* **Drivers**: `MFS100Dll.dll` & `MANTRA.MFS100.dll`

#### Why This Component Exists:
In modern web browsers, JavaScript running inside an intranet webpage (`http://server/CentralMFA/Gateway.aspx`) has **no direct USB hardware access** to read biometric devices plugged into an employee's workstation. 

To solve this completely offline without cloud services:
1. `MfaScannerAgent.exe` runs silently on each employee's PC.
2. It listens locally on `http://127.0.0.1:8034`.
3. It includes full **CORS (Cross-Origin Resource Sharing)** headers (`Access-Control-Allow-Origin: *`), allowing browser scripts from CentralMFA to communicate with it.

#### Endpoints Provided:
- `GET /mfs100/info`: Returns JSON `{ "Status": true, "DeviceModel": "Mantra MFS100", "Serial": "..." }`.
- `POST /mfs100/capture`: Initializes scanner, turns on the optical sensor light, waits for finger placement, and returns JSON `{ "Status": true, "IsoTemplate": "<base64>", "Quality": 86, "BitmapData": "<base64>" }`.

#### Deployment Script (`install.bat`):
- Double-clicking `install.bat` (Run as Administrator) copies the agent to `C:\Program Files\CentralMFA_Agent\` and creates a Windows Run registry entry (`HKLM\Software\Microsoft\Windows\CurrentVersion\Run`) so the scanner service starts automatically when employees log in.

---

## Core Engine Utilities (`/Utils/`)

### 13. Utils/DBHelper.cs - Oracle 11g Database Engine

* **Physical Location**: `CentralMFA/Utils/DBHelper.cs`
* **Technology**: `Oracle.ManagedDataAccess.Client` (100% managed ADO.NET, no Oracle Client or OCI required).

#### Key Responsibilities:
1. **Schema Auto-Initialization (`EnsureSchemaExists`)**:
   - On application startup, checks `USER_TABLES` in Oracle 11g.
   - If tables do not exist, automatically executes DDL to create `APP_USER_FINGERPRINTS`, `MFA_REGISTERED_APPS`, `MFA_APP_POLICIES`, `MFA_APP_USER_WHITELIST`, and `MFA_AUDIT_LOGS`, along with primary keys, foreign keys, and default seed records (`HRMS`, `ERP`, `PAYROLL`).
2. **Biometric BLOB Storage**:
   - `SaveFingerprint(pcno, templateBytes, quality)`: Performs an atomic `MERGE` query inserting or updating the raw biometric template BLOB in Oracle.
   - `GetFingerprint(pcno, out quality, out error)`: Reads the template BLOB directly into a `byte[]` array.
   - `HasFingerprint(pcno)`: Fast scalar check if an employee has enrolled.
3. **Application & Policy CRUD**:
   - Methods: `GetRegisteredApp(appId)`, `GetAllRegisteredApps()`, `SaveRegisteredApp(...)`, `DeleteRegisteredApp(appId)`.
   - `GetAppPolicy(appId)` & `SaveAppPolicy(appId, policy)`.
4. **User Whitelist CRUD**:
   - `GetWhitelistedUsers(appId)`, `AddUserToWhitelist(appId, pcno)`, `RemoveUserFromWhitelist(appId, pcno)`, `IsUserInWhitelist(appId, pcno)`.
5. **Audit Logging & Statistics**:
   - `LogAudit(appId, pcno, action, txnId, score, status, clientIp, details)`: Inserts a timestamped audit row.
   - `GetRecentAuditLogs(maxRows)`: Queries recent security events.
   - `GetDashboardStats(out totalApps, out activeApps, out enrolledUsers, out todayTotal, out todayApproved, out todayDenied)`: Single-query dashboard metrics aggregation.

---

### 14. Utils/DeviceManager.cs - Biometric Matching Engine

* **Physical Location**: `CentralMFA/Utils/DeviceManager.cs`

#### Key Responsibilities:
1. **Dynamic SDK Loading**:
   - Uses reflection to resolve `MANTRA.MFS100` types at runtime from `bin\MANTRA.MFS100.dll` or `Lib\MANTRA.MFS100.dll`.
   - Completely decouples the application from any specific compile-time SDK version, ensuring compatibility with whichever `MFS100Dll.dll` version is installed at your company.
2. **Template Matching (`MatchFingerprints`)**:
   - Takes `byte[] storedTemplate` (from Oracle) and `byte[] liveTemplate` (from client scanner).
   - Invokes `Device.MatchISO(storedTemplate, liveTemplate, ref matchScore)`.
   - Compares result against `MatchThreshold = 1400` (standard Mantra ISO 19794-2 false accept rate standard).
3. **Demo Template Generation**:
   - `GenerateDemoTemplate(pcno)`: Produces deterministic synthetic biometric vectors for offline simulation tests.

---

### 15. Utils/SecurityHelper.cs - Cryptographic Token & Replay Engine

* **Physical Location**: `CentralMFA/Utils/SecurityHelper.cs`

#### Key Responsibilities:
1. **HMAC-SHA256 Computation (`ComputeHmacSha256`)**:
   - Computes a keyed cryptographic hash for any message string using the application's secret key.
2. **Timing-Safe Signature Verification (`VerifyHmacSha256`)**:
   - Performs constant-time bitwise comparison (`expected[i] ^ signature[i]`) to protect against side-channel timing attacks.
3. **Replay Attack Prevention (`IsTimestampValid`)**:
   - Validates that the timestamp in the verification response is within **120 seconds** of current server time. Expired tokens are rejected.
4. **Cryptographic Nonces (`GenerateNonce`)**:
   - Generates high-entropy random hex tokens via `RNGCryptoServiceProvider`.
5. **Standardized Payload Formatting (`BuildPayload`)**:
   - Constructs the canonical pipe-delimited payload string:
     ```text
     appId|pcno|status|action|txnId|timestamp|nonce
     ```

---

## Database Schema Reference (`Mfa_Schema.sql`)

| Table Name | Description | Key Columns |
|---|---|---|
| **`APP_USER_FINGERPRINTS`** | Enrolled employee biometric templates | `PCNO` (PK), `FINGER_TEMPLATE` (BLOB), `QUALITY` (NUMBER), `UPDATED_ON` (DATE) |
| **`MFA_REGISTERED_APPS`** | Intranet applications allowed to use MFA | `APP_ID` (PK), `APP_NAME`, `BASE_URL`, `CALLBACK_URL`, `SECRET_KEY`, `IS_ACTIVE` |
| **`MFA_APP_POLICIES`** | Enforcement policy per application | `APP_ID` (PK/FK), `POLICY_TYPE` (`ALL_USERS`, `SPECIFIC_USERS`, `ENROLLED_ONLY`, `DISABLED`) |
| **`MFA_APP_USER_WHITELIST`** | Whitelisted PCNOs for `SPECIFIC_USERS` policy | `APP_ID` (PK/FK), `PCNO` (PK), `CREATED_ON` |
| **`MFA_AUDIT_LOGS`** | Live security verification audit trail | `LOG_ID` (PK), `APP_ID`, `PCNO`, `ACTION_NAME`, `TXN_ID`, `MATCH_SCORE`, `STATUS`, `CLIENT_IP`, `CREATED_ON` |

---

## Summary of Error Codes & Edge Case Handling

| Scenario | System Behavior | Gateway Feedback | Calling App Callback |
|---|---|---|---|
| **AppId Not Registered** | Rejects request before scanner step | Red Alert: *"Application ID is not registered"* | Does not redirect |
| **App is Inactive** | Rejects request | Alert: *"Application deactivated by administrator"* | Does not redirect |
| **Policy = DISABLED** | Auto-bypasses biometric scan | Bypasses instantly | Redirects with `status=APPROVED` (`MFA_POLICY_DISABLED`) |
| **User not in Whitelist (`SPECIFIC_USERS`)** | Auto-bypasses biometric scan | Bypasses instantly | Redirects with `status=APPROVED` (`EXEMPT_USER_POLICY`) |
| **User not Enrolled (`ENROLLED_ONLY`)** | Auto-bypasses biometric scan | Bypasses instantly | Redirects with `status=APPROVED` (`NO_ENROLLED_FINGERPRINT`) |
| **User not Enrolled (`ALL_USERS`)** | Blocks verification | Warning banner + Link to `Enroll.aspx?pcno=...` | User must enroll before proceeding |
| **Scanner Agent Offline** | Detects port 8034 offline | Amber Status Pill: *"Start MfaScannerAgent.exe on PC"* | Stays on Gateway until agent is started |
| **Fingerprint Mismatch** | Biometric score < 1400 | Red Alert: *"Match Score below threshold"* | Logs `DENIED` in Oracle; does not approve |
| **Replay / Stale Token (>120s)** | Calling app validates timestamp | N/A | Calling app rejects: *"Expired Token (>120s)"* |
| **URL Parameter Tampering** | Calling app checks HMAC | N/A | Calling app rejects: *"Cryptographic signature mismatch"* |
