# CentralMFA - Enterprise Biometric Multi-Factor Authentication & Management System

CentralMFA is a centralized, offline-ready Biometric Multi-Factor Authentication (MFA) gateway and administration portal designed for corporate intranet environments. It allows any intranet application (ASP.NET C#, PHP, Classic ASP, Java, etc.) to enforce fingerprint biometric authentication after standard LDAP/Active Directory login, or on sensitive submit-button transactions (such as payroll approval or purchase order release).

---

## Key Features

1. **Post-LDAP Login MFA**: After employees pass their usual LDAP/Active Directory login, the calling application redirects them to `Gateway.aspx`. CentralMFA captures their fingerprint from the local USB MFS100 scanner, validates it against templates stored in Oracle 11g, and securely redirects back with a cryptographically signed approval token.
2. **Submit-Button / Transaction Authorization**: Calling applications can trigger step-up biometric authentication on specific button clicks (e.g., `action=APPROVE_PAYROLL&txnId=TXN-101`) before executing critical database updates.
3. **Admin Management Portal**:
   - **Dashboard**: Real-time stats on registered apps, active apps, enrolled employees, and today's verification attempts.
   - **Application Registry**: Register intranet apps, manage unique client IDs, callback URLs, and generate 256-bit cryptographic shared secret keys.
   - **Enforcement Policies**: Configure per-app policies:
     - `ALL_USERS`: Every user must authenticate via fingerprint.
     - `SPECIFIC_USERS`: Only whitelisted PCNOs are prompted; others bypass automatically.
     - `ENROLLED_ONLY`: Only users with registered fingerprints in Oracle are prompted; unenrolled bypass.
     - `DISABLED`: MFA is completely bypassed for the app.
   - **User Mapping / Whitelist**: Map specific employee PCNOs to applications when `SPECIFIC_USERS` policy is active.
   - **Biometric Audit Trail**: Searchable, timestamped security logs capturing every login and transaction attempt, match score, client IP, and approval/denial status.
4. **Client-Side Scanner Communication (Port 8034)**: Because Mantra MFS100 scanners are plugged into employee client PCs, browser JavaScript talks to a lightweight local background agent (`http://127.0.0.1:8034`) to capture biometric ISO templates directly.
5. **High-Tech Biometric UI & Auto-Scan on Load**:
   - Realistic SVG fingerprint ridges with an animated cyan laser scanning beam.
   - **Auto-Scan on Load**: When an employee navigates to the Gateway with their finger already on the scanner, it automatically initiates capture after a smooth 1.5-second countdown.
   - **Manual Touch & Scan**: Alternatively, employees keep their finger pressed on the sensor glass and click the large **"Scan Fingerprint"** button.
   - Live LED status pill (🟢 Ready, 🔵 Scanning, 🟡 Initializing, 🔴 Offline) and real-time capture quality progress bar.
6. **Tamper-Proof HMAC-SHA256 Security**: All redirects back to calling applications are signed with a keyed HMAC-SHA256 hash using the app's secret key, complete with nonces and a 120-second timestamp freshness window to prevent URL tampering and replay attacks.
7. **100% Offline / Intranet Native**: Zero external CDN, Google Fonts, or internet dependencies.

---

## Directory Structure

```text
e:\fingerprint_scanner\CentralMFA\
├── Admin\
│   ├── Login.aspx / .cs / .designer.cs       # Admin authentication (Web.config credentials)
│   ├── Dashboard.aspx / .cs / .designer.cs   # System metrics and activity summary
│   ├── Apps.aspx / .cs / .designer.cs        # App registration & secret key generation
│   ├── Policies.aspx / .cs / .designer.cs    # Per-application MFA enforcement policies
│   ├── UserMapping.aspx / .cs / .designer.cs # Employee PCNO whitelisting
│   └── AuditLogs.aspx / .cs / .designer.cs   # Biometric verification audit trail
├── ClientAgent\
│   ├── MfaScannerAgent.exe                   # Local PC background scanner service (Port 8034)
│   ├── MFS100Dll.dll                         # Native Mantra driver DLL
│   ├── MANTRA.MFS100.dll                     # Managed Mantra SDK wrapper
│   └── install.bat                           # Auto-start setup script for client PCs
├── Lib\
│   ├── Oracle.ManagedDataAccess.dll          # 100% managed Oracle driver (no Oracle Client needed)
│   ├── MANTRA.MFS100.dll                     # Managed SDK wrapper
│   ├── x86\MFS100Dll.dll                     # 32-bit native driver
│   └── x64\MFS100Dll.dll                     # 64-bit native driver
├── Samples\
│   ├── SampleCallingApp_CSharp.aspx / .cs    # ASP.NET C# integration reference
│   ├── sample_calling_app_php.php            # PHP integration reference
│   └── sample_calling_app_asp.asp            # Classic ASP (VBScript) integration reference
├── Static\
│   ├── app.css                               # Clean corporate CSS styles (offline)
│   └── mfa-client.js                         # Scanner communication script (Port 8034)
├── Utils\
│   ├── DBHelper.cs                           # Oracle 11g ADO.NET helper & schema auto-creator
│   ├── DeviceManager.cs                      # Biometric matching engine (MatchISO)
│   └── SecurityHelper.cs                     # HMAC-SHA256 signing, validation & nonces
├── Enroll.aspx / .cs / .designer.cs          # Biometric employee enrollment page
├── Gateway.aspx / .cs / .designer.cs         # Central MFA gateway verification page
├── Global.asax / .cs                         # App startup & schema verification
├── Mfa_Schema.sql                            # Oracle 11g database DDL & sample data
├── Web.config                                # Oracle connection string & admin credentials
├── CentralMFA.csproj                         # MSBuild project file (.NET 4.5)
└── CentralMFA.sln                            # Visual Studio 2015/2019/2022 solution
```

---

## 1. Database Setup (Oracle 11g)

If running in your company intranet with an existing Oracle 11g database:
1. Open SQL*Plus, SQL Developer, or Toad.
2. Execute [Mfa_Schema.sql](file:///e:/fingerprint_scanner/CentralMFA/Mfa_Schema.sql) against your application schema:
   - `APP_USER_FINGERPRINTS`: Stores `PCNO`, `FINGER_TEMPLATE` (BLOB), `QUALITY`, and audit dates.
   - `MFA_REGISTERED_APPS`: Stores `APP_ID`, `APP_NAME`, `CALLBACK_URL`, `SECRET_KEY`, `IS_ACTIVE`.
   - `MFA_APP_POLICIES`: Stores `APP_ID`, `POLICY_TYPE` (`ALL_USERS`, `SPECIFIC_USERS`, `ENROLLED_ONLY`, `DISABLED`).
   - `MFA_APP_USER_WHITELIST`: Stores `APP_ID` and `PCNO` mappings for user-specific enforcement.
   - `MFA_AUDIT_LOGS`: Stores timestamped logs of every verification attempt.
3. Update [Web.config](file:///e:/fingerprint_scanner/CentralMFA/Web.config) with your Oracle connection string:
   ```xml
   <connectionStrings>
     <add name="OracleDB" 
          connectionString="User Id=mfa_user;Password=mfa_pass;Data Source=//oracle-server:1521/ORCL;" 
          providerName="Oracle.ManagedDataAccess.Client" />
   </connectionStrings>
   ```
   *(Note: `DBHelper.cs` also automatically executes `EnsureSchemaExists()` on startup, creating missing tables dynamically if they do not exist).*

---

## 2. Admin Portal Usage

1. Open `http://<server-ip>/CentralMFA/Admin/Login.aspx`.
2. Default credentials:
   - **Username**: `admin`
   - **Password**: `admin123` *(Configurable in `Web.config` appSettings `AdminUsername` and `AdminPassword`)*.
3. **Registering Calling Apps**:
   - Go to **Applications** (`Admin/Apps.aspx`).
   - Enter `AppId` (e.g., `ERP`), `App Name`, `Base URL`, and `Callback URL`.
   - Click **Generate 256-bit Key** to produce a secure cryptographic secret.
   - Click **Save Application**.
4. **Setting Enforcement Policies**:
   - Go to **Policies** (`Admin/Policies.aspx`).
   - Select an application.
   - Choose policy: `ALL_USERS`, `SPECIFIC_USERS`, `ENROLLED_ONLY`, or `DISABLED`.
   - If `SPECIFIC_USERS` is selected, click the link to **User Mapping** to add specific employee PCNOs.

---

## 3. Client PC Setup (Employee Workstations)

Because employee MFS100 scanners are plugged into their individual workstations via USB, browser JavaScript running on the gateway needs to read the local scanner.

Deploy the `CentralMFA/ClientAgent/` folder to employee PCs:
1. Copy the folder to `C:\Program Files\CentralMFA_Agent\` (or any preferred folder).
2. Ensure `MFS100Dll.dll` in this folder matches the version that works on the employee machine.
3. Double click `install.bat` (Run as Administrator) to configure `MfaScannerAgent.exe` to run automatically at Windows logon.
4. `MfaScannerAgent.exe` runs silently in the background, listening on `http://127.0.0.1:8034` with full CORS support.

---

## 4. Calling Application Integration Guide

Any intranet application (ASP.NET, PHP, Classic ASP) can integrate using the steps below.

### A. Post-LDAP Login MFA Flow

1. Employee enters Windows AD / LDAP credentials on the calling app's login page.
2. The calling app validates LDAP credentials.
3. Once valid, the calling app generates a secure redirect to CentralMFA:
   ```text
   http://<central-mfa-host>/CentralMFA/Gateway.aspx?appId=ERP&pcno=EMP001&returnUrl=http://<app-host>/Callback.aspx&action=LOGIN&timestamp=<epoch>&nonce=<nonce>&sig=<hmac>
   ```
4. CentralMFA checks application policy:
   - If policy allows bypass (e.g. `DISABLED`, or `SPECIFIC_USERS` and user is not whitelisted), CentralMFA immediately redirects back with `status=APPROVED`.
   - If MFA is required, CentralMFA prompts the user to place their finger on the MFS100 scanner.
5. CentralMFA matches the scanned template against Oracle 11g.
6. CentralMFA redirects the browser back to `returnUrl` with:
   ```text
   http://<app-host>/Callback.aspx?status=APPROVED&appId=ERP&pcno=EMP001&action=LOGIN&txnId=&score=3850&timestamp=1789650031&nonce=2f3dba27046efd9ed4e9ee39&sig=5e1223fbb4887826f12065c6cc72094aa30828dce965457f3304930524a5c543
   ```
7. The calling app verifies the HMAC-SHA256 signature using its secret key and starts the employee session.

### B. Sensitive Submit-Button / Step-Up Authorization Flow

When an employee clicks a critical submit button (e.g., "Approve Payroll" or "Release Purchase Order"):
1. The calling app specifies `action` and `txnId`:
   ```text
   http://<central-mfa-host>/CentralMFA/Gateway.aspx?appId=PAYROLL&pcno=EMP001&action=APPROVE_PAYROLL&txnId=TXN-2026-9912&returnUrl=http://<app-host>/PayrollCallback.aspx
   ```
2. Gateway displays the action and transaction reference to the user:
   > **Action**: `APPROVE_PAYROLL`  
   > **Transaction ID**: `TXN-2026-9912`  
   > *Please place your finger on the scanner to authorize this action.*
3. CentralMFA verifies the fingerprint and redirects back with `status=APPROVED`.
4. The calling app validates the signature and executes the database update.

---

## 5. Cryptographic Signature Formula

To prevent users from modifying query string parameters (such as changing `status=DENIED` to `status=APPROVED`), all responses include an HMAC-SHA256 signature.

### Canonical Payload String:
```text
payload = appId + "|" + pcno + "|" + status + "|" + action + "|" + txnId + "|" + timestamp + "|" + nonce
```

### Signature:
```text
sig = HMAC-SHA256(payload, secretKey)   // Lowercase or uppercase hex
```

### Verification Rules:
1. Compute `expectedSig = HMAC-SHA256(payload, secretKey)`.
2. Compare `expectedSig` and `sig` using a constant-time / timing-safe string comparison.
3. Check `timestamp`: ensure `abs(currentEpoch - timestamp) <= 120` (within 2 minutes).

---

## 6. Code Examples for Calling Applications

### ASP.NET C# (Web Forms or MVC)
See working sample: [Samples/SampleCallingApp_CSharp.aspx](file:///e:/fingerprint_scanner/CentralMFA/Samples/SampleCallingApp_CSharp.aspx) and [Samples/SampleCallingApp_CSharp.aspx.cs](file:///e:/fingerprint_scanner/CentralMFA/Samples/SampleCallingApp_CSharp.aspx.cs).

```csharp
// Callback handling in ASP.NET C#
string status    = Request.QueryString["status"];
string appId     = Request.QueryString["appId"];
string pcno      = Request.QueryString["pcno"];
string action    = Request.QueryString["action"];
string txnId     = Request.QueryString["txnId"];
string timestamp = Request.QueryString["timestamp"];
string nonce     = Request.QueryString["nonce"];
string sig       = Request.QueryString["sig"] ?? Request.QueryString["signature"];

string payload = SecurityHelper.BuildPayload(appId, pcno, status, action, txnId, timestamp, nonce);
bool isValid = SecurityHelper.VerifyHmacSha256(payload, sig, APP_SECRET);

if (isValid && SecurityHelper.IsTimestampValid(timestamp, 120) && status == "APPROVED")
{
    // Biometric authentication verified! Proceed with login or transaction.
}
```

### PHP
See working sample: [Samples/sample_calling_app_php.php](file:///e:/fingerprint_scanner/CentralMFA/Samples/sample_calling_app_php.php).

```php
$dataToVerify = "{$appId}|{$pcno}|{$status}|{$action}|{$txnId}|{$timestamp}|{$nonce}";
$expectedSig  = hash_hmac('sha256', $dataToVerify, APP_SECRET);

if (hash_equals(strtolower($expectedSig), strtolower($sig))) {
    if (abs(time() - (int)$timestamp) <= 120 && strcasecmp($status, 'APPROVED') === 0) {
        // Biometric MFA Succeeded!
    }
}
```

### Classic ASP (VBScript)
See working sample: [Samples/sample_calling_app_asp.asp](file:///e:/fingerprint_scanner/CentralMFA/Samples/sample_calling_app_asp.asp).

---

## 7. Building & Deploying to Production IIS

1. **Compilation**:
   Open `CentralMFA.sln` in Visual Studio 2015, 2017, 2019, or 2022 and select **Build > Build Solution** (or run MSBuild from command line):
   ```cmd
   msbuild CentralMFA.csproj /p:Configuration=Release
   ```
2. **IIS Setup**:
   - Create a new Web Site or Application in IIS pointing to `e:\fingerprint_scanner\CentralMFA`.
   - Ensure the Application Pool is set to `.NET CLR Version v4.0` (Pipeline: Integrated).
   - If running in 32-bit IIS App Pool: Set **Enable 32-Bit Applications = True** in Advanced Settings.
   - If running in 64-bit IIS App Pool: Set **Enable 32-Bit Applications = False**.
   - `Oracle.ManagedDataAccess.dll` is pure AnyCPU managed code and works identically on both 32-bit and 64-bit pools.
