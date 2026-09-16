# Authentication - Offline Multi-Factor Authentication (MFA) Web Application
**ASP.NET WebForms (.NET Framework 4.5 / Visual Studio 2015+)**  
**Active Directory Credentials + Mantra MFS100 Fingerprint Scanner + Oracle 11g Database**

---

## 1. Overview

This application provides a complete, self-contained, two-factor authentication (MFA) intranet solution designed to operate in **100% offline environments** without internet access:
- **Factor 1 (Active Directory)**: User logs in using domain credentials (`SAMAccountName` / Password). The system authenticates against Active Directory LDAP and extracts their `EmployeeID` (or `PCNO`).
- **Factor 2 (Biometric Fingerprint)**: User places their registered finger on the Mantra MFS100 optical fingerprint scanner. The application fetches the enrolled ISO 19794-2 template `BLOB` from **Oracle 11g** for that specific `PCNO`, performs high-precision biometric matching (`MatchISO`), and validates access.
- **Fingerprint Enrollment (`Enroll.aspx`)**: A dedicated open registration screen to capture fingerprints from the Mantra scanner and store them directly into Oracle 11g.

---

## 2. Key Highlights & Offline Guarantee

1. **Zero External Dependencies / CDNs**:
   - All styling (`Static/app.css`) is stored locally.
   - No external Google Fonts, FontAwesome CDNs, or remote JavaScript files are used.
2. **Oracle 11g Managed Data Access**:
   - Bundles `Oracle.ManagedDataAccess.dll` in `Lib/` and `bin/`.
   - **No Oracle Client** needs to be installed on client or server machines.
3. **Mantra MFS100 Scanner & Demo Mode**:
   - Supports live Mantra MFS100 USB hardware.
   - Includes a **Demo Mode toggle** for testing all AD login, enrollment, and fingerprint verification flows without needing the physical USB scanner plugged in.
4. **Active Directory Offline Bypass Accounts**:
   - For offline development without a domain controller, accounts `1001`, `1002`, `1003`, and `admin` are available as instant test accounts.

---

## 3. Project Structure

```text
Authentication/
├── Authentication.sln         # Visual Studio 2015 Solution file
├── Authentication.csproj        # .NET Framework 4.5 WebForms Project file
├── Web.config                 # Oracle connection strings & AD LDAP path
├── Global.asax / .cs          # App lifecycle & automatic Oracle table check
├── Create_Tables.sql          # Oracle 11g DDL script
│
├── Login.aspx / .cs           # Two-Factor MFA Authentication portal
├── Enroll.aspx / .cs          # Open Fingerprint Enrollment & Management
├── Success.aspx / .cs         # MFA Login Success verification screen
│
├── Static/
│   └── app.css                # 100% offline modern responsive stylesheet
│
├── Utils/
│   ├── ADHelper.cs            # Active Directory authentication & PCNO extractor
│   ├── DBHelper.cs            # Oracle 11g ADO.NET helper & BLOB template manager
│   └── DeviceManager.cs       # Mantra MFS100 SDK wrapper & Demo simulator
│
├── Lib/
│   ├── Oracle.ManagedDataAccess.dll
│   ├── MANTRA.MFS100.dll
│   └── native/
│       ├── x86/ (MFS100Dll.dll, iengine_ansi_iso.dll)
│       └── x64/ (MFS100Dll.dll, iengine_ansi_iso.dll)
└── bin/                       # Compiled outputs and native DLLs
```

---

## 4. Database Setup (Oracle 11g)

The application will **automatically create** the required table `APP_USER_FINGERPRINTS` on application startup if the database user has table creation privileges. 

Alternatively, you can manually execute the provided [`Create_Tables.sql`](file:///e:/fingerprint_scanner/Authentication/Create_Tables.sql) script in SQL*Plus or SQL Developer:

```sql
CREATE TABLE APP_USER_FINGERPRINTS (
    PCNO            VARCHAR2(50)   NOT NULL,
    FINGER_TEMPLATE BLOB           NOT NULL,
    QUALITY         NUMBER(3)      DEFAULT 0,
    CREATED_ON      DATE           DEFAULT SYSDATE,
    UPDATED_ON      DATE           DEFAULT SYSDATE,
    CONSTRAINT PK_APP_USER_FINGERPRINTS PRIMARY KEY (PCNO)
);
```

### Configuring `Web.config`
Open [`Web.config`](file:///e:/fingerprint_scanner/Authentication/Web.config) and configure your Oracle 11g credentials and AD path:

```xml
<connectionStrings>
  <add name="OracleDB" 
       connectionString="User Id=your_user;Password=your_password;Data Source=//your_server:1521/your_service_name;Min Pool Size=2;Max Pool Size=100;" 
       providerName="Oracle.ManagedDataAccess.Client" />
</connectionStrings>

<appSettings>
  <!-- Replace with your company Active Directory LDAP server -->
  <add key="ADConnectionPath" value="LDAP://192.168.0.106/DC=ad01,DC=yajnesh,DC=com" />
  <!-- Fingerprint match threshold (1400 = standard Mantra biometric threshold) -->
  <add key="FingerprintMatchThreshold" value="1400" />
</appSettings>
```

---

## 5. How to Run & Test

### In Visual Studio 2015 / 2017 / 2019 / 2022
1. Open `Authentication.sln`.
2. Right-click `Login.aspx` → **Set As Start Page**.
3. Press **F5** (or **Ctrl + F5** to start without debugging).

### Testing Step-by-Step (Without Physical Scanner)
1. **Enroll a Test Fingerprint**:
   - Open navigation link: **Fingerprint Enrollment** (`Enroll.aspx`).
   - Check **Demo Mode** (checked automatically if no physical scanner is detected).
   - In **Employee ID / PCNO**, type: `1001`.
   - Click **1. Capture Fingerprint** (simulates capture and shows template size ~400 bytes and quality 85%).
   - Click **2. Save to Oracle Database** (stores the BLOB in `APP_USER_FINGERPRINTS`).
2. **Perform Two-Factor Authentication**:
   - Open **MFA Login** (`Login.aspx`).
   - **Step 1 (AD Login)**: Type Username `1001` (or test account `admin`) and any password. Click **Next: Verify Active Directory**.
   - **Step 2 (Biometric Scan)**: The screen updates to show `PCNO: 1001`. Click **Scan Fingerprint & Complete Login**.
   - The system retrieves the template from Oracle 11g, performs biometric comparison, and redirects to `Success.aspx` with a match score (e.g. 3850 / 1400).

---

## 6. Resolving `PInvokeStackImbalance` (Visual Studio Debugging)

If you debug using physical Mantra MFS100 hardware in 32-bit IIS Express under Visual Studio F5:
- **Option A (Recommended)**: In Visual Studio, go to **Tools** → **Options** → **Projects and Solutions** → **Web Projects** → check **Use the 64-bit version of IIS Express**, copy `Lib/native/x64/*` to `bin/`, and run.
- **Option B**: In Visual Studio, open **Debug** → **Windows** → **Exception Settings** (<kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>E</kbd>) → uncheck `PInvokeStackImbalance` under **Managed Debugging Assistants**.
- **Option C**: Run with <kbd>Ctrl</kbd> + <kbd>F5</kbd> (**Start Without Debugging**).
