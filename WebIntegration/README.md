# Fingerprint after LDAP login: integrating with ASP.NET WebForms

```
User PC                                   Web server (IIS)
-------                                   ----------------
Mantra driver + FpScannerAgent            Your WebForms app
(background, localhost:8036,                + FingerprintMfa.dll
 capture only, no DB, no secrets)           + FingerprintVerify.aspx / FingerprintAdmin.aspx / FingerprintMfa.ashx
                                            + Oracle connection, table names, matching  (Web.config, encrypted)

1. User logs in with LDAP on Login.aspx            -> OK
2. Login.aspx calls MfaLogin.BeginLogin(...)        -> fingerprint enabled for this PC No?  no -> normal login
3. FingerprintVerify.aspx: the browser asks the agent on the PC to capture (light blinks, 10 s)
4. The template is posted to the SERVER, which matches it against Oracle for the PC No it got from LDAP
5. Match -> user signed in (+ your SignedIn code) -> original page.  3 failures -> back to login.
```

The user's PC never sees the connection string or table names, and it can't fake a "verified" result, because only the server decides.

---

## A. One time: database (Oracle 11g R2+)
1. Run `Sql/Create_MFA_Tables.sql`. It creates `FP_MFA_USERS` (who must give a fingerprint) and `FP_MFA_AUDIT` plus its sequence (every attempt and admin change).
2. The view with the enrolled fingerprints (`FP_AUTH_V`, or `FP_POC_ENROLL` from the POC) must be readable by the web app's DB user.

## B. Build (no Visual Studio needed)
```
WebIntegration\build.bat     -> dist\WebIntegration\   (files for the web app)
ScannerAgent\build.bat       -> dist\FpScannerAgentSetup\  (installer for user PCs)
```
Or open `FingerprintAuthentication.sln` in Visual Studio.

## C. Add it to your web application (per application)
1. **Copy** from `dist\WebIntegration\`:
   * `bin\FingerprintMfa.dll` and `bin\Oracle.ManagedDataAccess.dll` (skip Oracle if the app already has it) go into the app's `bin\`.
   * `bin\mantra\` (the whole folder) goes into the app's `bin\mantra\`. It holds the Mantra matcher for 32-bit and 64-bit app pools.
   * `FingerprintVerify.aspx`, `FingerprintAdmin.aspx` and `FingerprintMfa.ashx` go into the app root. They have no code-behind, so you don't need to recompile.
   * Zip `dist\FpScannerAgentSetup\` and save it as `Downloads\FpScannerAgentSetup.zip` in the app. The fingerprint page offers it when the agent is missing.
2. **Edit Web.config.** Add the parts from `Samples/Web.config.additions.xml`. The important keys are:
   * `FpMfa.SourceTable` / `PcnoColumn` / `TemplateColumn`: where the fingerprints are, with the same values as in the POC.
   * `FpMfa.MatchThreshold`: the value that worked in the POC.
   * `FpMfa.AdminUsers`: the PC Nos allowed to use the admin page.
   * `FpMfa.SignInMode`:
     * `Forms` if your app uses Forms authentication.
     * `Session` if it checks e.g. `Session["PCNO"]` on every page.
     * `None` if you sign the user in yourself in the `SignedIn` event.
   * The `<location>` entries, so the fingerprint page works before the user is signed in.
3. **Change the Login page.** See `Samples/Login.aspx.cs.sample`. After the LDAP check succeeds, add:
   ```csharp
   if (FingerprintMfa.MfaLogin.BeginLogin(Context, pcno, Request.QueryString["ReturnUrl"]))
       return;   // user goes to the fingerprint page
   // ...your existing login code (fingerprint not required for this user)
   ```
   Then, in `Global.asax` `Application_Start`, run your "after login" code for users who passed the fingerprint step:
   ```csharp
   FingerprintMfa.MfaLogin.SignedIn += (ctx, pcno) => Login.CompleteLogin(ctx, pcno);
   ```
   ⚠️ Don't set the "logged in" session value or auth cookie **before** `BeginLogin`. Otherwise the user is already logged in without the fingerprint.
4. **Encrypt the connection string** on the server, from an admin command prompt:
   ```
   %WINDIR%\Microsoft.NET\Framework64\v4.0.30319\aspnet_regiis -pef "connectionStrings" "D:\inetpub\YourApp"
   ```
   ASP.NET decrypts it automatically. To edit it later, decrypt with `-pdf` and encrypt again.
5. **Check the IIS app pool.** It works in 32-bit and 64-bit pools, because the right Mantra native DLLs are picked automatically.

## D. Admin: who must give a fingerprint
Open `https://yourapp/FingerprintAdmin.aspx`, logged in as a PC No listed in `FpMfa.AdminUsers`.
* Enter a PC No and click **Enable** or **Disable**. You can add a remark.
* The user list shows each PC No's **enrolled fingers**. Enabling a user with 0 fingers shows a warning, because they couldn't log in.
* **Recent activity** shows every verification, block and admin change, with score and IP.

Modes (Web.config):

| Setting | Effect |
|---|---|
| `FpMfa.Enabled=false` | Fingerprint is switched off for the whole application. |
| `FpMfa.RequireForAllUsers=false` (default) | Only users **enabled** on the admin page are asked. |
| `FpMfa.RequireForAllUsers=true` | Everyone is asked. |
| `FpMfa.AllowIfNotEnrolled=true` | Enabled users with no enrolled fingerprint skip the step (useful during rollout). |

## E. User PCs: one-time setup
Users run **`Setup.bat`** from `FpScannerAgentSetup.zip` once (see `ScannerAgent/README.md`). It needs admin, for the driver and Program Files.
* It installs the Mantra driver (asks Y/N) and the agent to `C:\Program Files\FpScannerAgent`.
* The agent starts **hidden at every logon for every user** and runs in the background always.
* It registers the `fpscanneragent://` link, so the fingerprint page's **"Start service"** button can restart the agent.

Self-service: when the agent isn't installed or not running, the fingerprint page shows **Start service** and **Install (one time)**. It continues automatically once the agent answers.

**Before rolling out,** set `AllowedOrigins` in `Agent\FpScannerAgent.exe.config` inside the setup folder to your app address(es), for example `https://txn.company.local`. Then no other website can use the scanner.

## F. Check before go-live
1. **Server matching without a scanner.** The server has no scanner attached, and some Mantra SDK builds refuse to match without one. Test one login. If the fingerprint page says *"Fingerprint matching is not available on the server"*, check `FP_MFA_AUDIT.MESSAGE`. Tell me the error, and a different server-side matcher can be plugged into `ServerMatcher.cs`.
2. **Disable for yourself first,** and keep one admin PC No disabled until everything is verified, so you can't lock yourself out.
3. **Failures are blocked, not let through.** If the database is down, users who are enabled for fingerprint can't log in, and the page asks them to contact the administrator. Users who aren't enabled log in normally, unless `RequireForAllUsers=true`, in which case everyone is blocked.

## Files
| File | Purpose |
|---|---|
| `FingerprintMfa/*.cs` | Server module (`FingerprintMfa.dll`): config, Oracle access, Mantra matcher, login flow, JSON handler |
| `CopyToWebApp/FingerprintVerify.aspx` | Fingerprint step after LDAP |
| `CopyToWebApp/FingerprintAdmin.aspx` | Admin page |
| `CopyToWebApp/FingerprintMfa.ashx` | Endpoint used by both pages |
| `Sql/Create_MFA_Tables.sql` | `FP_MFA_USERS`, `FP_MFA_AUDIT` (Oracle 11g) |
| `Samples/` | Web.config additions, Login / Global.asax changes |
