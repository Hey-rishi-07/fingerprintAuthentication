# Fingerprint Verification POC (Mantra MFS100 + Oracle)

**Folder layout**
```
FingerprintVerifyPOC.sln      <- open this in Visual Studio
*.cs, App.config              program source + settings
Lib\                          Mantra SDK + Oracle driver DLLs (copied next to the exe on build)
Driver\MFS100_Driver_Setup.exe scanner driver for each PC
Sql\Create_POC_View.sql       view over the main fingerprint table
Sql\Create_POC_Enroll_Table.sql  the POC's own enrolment table + porting existing data
Deploy\*.bat                  run / install / uninstall scripts (copied into dist\)
Docs\SCANNER_TROUBLESHOOTING.md
build.bat                     build without Visual Studio -> dist\
```

This program verifies an employee against the fingerprints that are **already enrolled** in the main Oracle biometric table. It never writes to the main table. For independent testing it can also enrol fingers into its own table, `FP_POC_ENROLL` (see 1b).

```
PC No entered ─► read ALL enrolled fingers for that PCNO (Oracle view)
             ─► scanner light blinks ─► light stays ON, waits up to 10 s for a finger
             ─► live finger matched against every enrolled finger (best score wins)
             ─► AUTHENTICATION SUCCESSFUL / FAILED
```

## Why it is a small local program even for the web version

A browser cannot open a USB device, so **every** web fingerprint solution needs a small program on the user's PC that drives the scanner. This program is that piece, and it already gives you the web flow:

| Mode | How to start | Use |
|---|---|---|
| **Web** (default) | `FingerprintVerifyPOC.exe` or `run_web.bat` | Opens `http://localhost:8035/` in the browser. This is the page to show your head. |
| Console | `FingerprintVerifyPOC.exe --console` or `run_console.bat` | Terminal demo: type a PC No and get the result. |
| Enrol (console) | `FingerprintVerifyPOC.exe --enroll` or `run_enroll_console.bat` | Enrol fingers into the POC table. |
| Import | `FingerprintVerifyPOC.exe --import ALL` or `import_existing.bat` | Copy existing templates into the POC table. |
| Scripted | `FingerprintVerifyPOC.exe --pcno 123456` | One verification. Prints JSON and returns an exit code (below). |

Later, your **transaction web application** calls the same local service from JavaScript (see [Web integration](#web-integration-for-the-transaction-application)).

## 1. Database (one time, DBA)

Run `Sql/Create_POC_View.sql` after filling in the real names. It creates the view `FP_AUTH_V (PCNO, FINGER_NO, FINGER_TEMPLATE)` with one row per enrolled finger, so several rows per PCNO are fine. It also sets up an optional read-only user that only has `SELECT` on that view.

**First, check the template format** (the query is in the SQL file). The MFS100 SDK can only match **ISO 19794-2** (`MatchISO`) or **ANSI 378** (`MatchANSI`) templates:

* The header starts with `464D5200` ("FMR"): the format is supported. Set `TemplateFormat` to ISO or ANSI.
* Anything else means the main table holds images or a vendor-specific template from different software, and this SDK cannot match it. Find out which device and software enrolled the fingerprints before the demo.

Templates stored as text (Base64 or HEX in CLOB/VARCHAR2) also work (`TemplateEncoding`).

## 1b. Independent testing: the POC's own enrolment table (`FP_POC_ENROLL`)

To test without depending on the main table, enrol fingers into a separate table. Oracle 11g R2 or newer is supported.

1. **Create the table.** Run `Sql/Create_POC_Enroll_Table.sql`, or let the program create it on start if the DB user has CREATE TABLE. The DB user needs SELECT/INSERT/UPDATE/DELETE on it. The table holds one row per (PCNO, finger).
2. **Enrol.** Open `http://localhost:8035/enroll` (there's a link on the main page), or use `run_enroll_console.bat`.
   1. Enter the PC No and choose the finger.
   2. The light blinks. Place the finger, lift it, then place the same finger again.
   3. The two scans must match each other, which ensures it's the same finger placed properly. Then the better-quality one is saved.
   4. The page lists that PC No's fingers and can delete them.
3. **Port existing test data** (optional). Run `import_existing.bat` (all PC Nos) or `import_existing.bat 123456` (one PC No). It reads from `ImportSourceTable` (default `FP_AUTH_V`) through the same decoder, so BLOB, Base64 and HEX all work. Rows that aren't ISO/ANSI templates are skipped and listed. The SQL file also has a plain-SQL copy for raw-BLOB sources.
4. **Verify against it.** Set `<add key="FpSourceTable" value="FP_POC_ENROLL" />` in the `.config` and restart the program. Set it back to `FP_AUTH_V` to verify against the main table again.

The main fingerprint table is never written to. Enrolment requests are only accepted from the local page, never from another website. To turn the Enroll page off, set `EnrollEnabled=false`.

## 2. What to install on the test PC

| # | Item | Notes |
|---|---|---|
| 1 | **Windows 10/11** (Windows 7 SP1 also works) | |
| 2 | **.NET Framework 4.5 or newer** | Already present on Windows 10/11. |
| 3 | **Mantra MFS100 driver** | `Driver\MFS100_Driver_Setup.exe` in this folder. Needs admin, once per PC. Plug in the scanner afterwards. |
| 4 | **Stop the Mantra RD Service / AVDM** if it is installed | It locks the scanner (Init error `-1`). See `Docs/SCANNER_TROUBLESHOOTING.md`. |
| 5 | **Network access to Oracle** (port 1521) | **No Oracle Client install needed**: `Oracle.ManagedDataAccess.dll` is shipped with the program. |
| 6 | The `dist` folder of this program | Contents are listed below. |

You **do not** need Visual Studio, IIS, or an Oracle client on the test PC.

## 3. Build (on any Windows PC, no Visual Studio needed)

```bat
cd FingerprintVerifyPOC
build.bat
```

This uses the C# compiler that ships with .NET Framework (`C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe`) and produces `dist\`:

```
FingerprintVerifyPOC.exe            FingerprintVerifyPOC.exe.config   <- edit this
Oracle.ManagedDataAccess.dll        MANTRA.MFS100.dll
MFS100Dll.dll  iengine_ansi_iso.dll (32-bit native SDK)
run_web.bat  run_console.bat  install.bat  uninstall.bat
```

Or open `FingerprintVerifyPOC.sln` in Visual Studio and press Ctrl+F5. It is built as x86 because the MFS100 native DLLs are 32-bit.

## 4. Configure: `FingerprintVerifyPOC.exe.config`

| Key | Default | Meaning |
|---|---|---|
| `OracleDB` connection string | — | `User Id=..;Password=..;Data Source=//host:1521/SERVICE;` |
| `FpSourceTable` / `FpPcnoColumn` / `FpTemplateColumn` / `FpFingerColumn` | `FP_AUTH_V` / `PCNO` / `FINGER_TEMPLATE` / `FINGER_NO` | Where the enrolled fingerprints are read from. |
| `TemplateFormat` | `AUTO` | `AUTO` tries MatchISO and MatchANSI and keeps the best score. `ISO` / `ANSI` restrict it to one. |
| `TemplateEncoding` | `AUTO` | `RAW` (BLOB), `BASE64`, `HEX`, or `AUTO`. |
| `MatchThreshold` | `1400` | Minimum score to accept. Calibrate it during the POC (see below). |
| `CaptureTimeoutSeconds` | `10` | How long the light waits for a finger. |
| `MinQuality` | `40` | A lower-quality capture asks the user to place the finger again (within the 10 s). |
| `BlinkCount` / `BlinkOnMs` / `BlinkOffMs` / `BlinkSettleMs` | `3` / `250` / `250` / `700` | The blink before capture, and the pause after it so the sensor is stable. Set `BlinkCount` to `0` to just switch the light on. |
| `HttpPort` | `8035` | Local web service port. |
| `AllowedOrigins` | `*` | Later, list only the transaction web app URL(s). |

### About the blink

The MFS100 SDK has **no LED API**. The red light is on only while the sensor is capturing. The blink is made by starting and stopping a capture a few times, and then the light stays on for the 10-second wait (standard behaviour). If the blink misbehaves on a particular driver version, set `BlinkCount` to `0`.

### When fingerprints do not match: run the diagnostics

Run `run_diagnostics.bat` (or `FingerprintVerifyPOC.exe --diag 123456`), enter the PC No, and place the same finger twice. It prints, and saves to `logs\diag_*.txt`:

1. **The stored templates for that PC No and their format.** It reports ISO, ANSI, Base64/HEX text, or an unknown format that the MFS100 SDK can't match.
2. **The live capture's quality and format.**
3. **A matcher self-test.** The live template is matched against itself, which must give a high score.
4. **MatchISO and MatchANSI scores** against every stored finger.
5. **A live-versus-live score for the same finger.** This shows what a real match scores with this matcher, so you can set `MatchThreshold`.
6. **A plain-language explanation** of the likely cause.

### Calibrating `MatchThreshold`

Every attempt is logged to `logs\verify_YYYYMMDD.csv` with the score, quality, and number of fingers checked. During the POC:
* Have a few people verify with their own finger, and note the scores.
* Have them try someone else's PC No (impostor attempts), and note those scores.
* Set the threshold well above the impostor scores and below the genuine scores.

## 5. Run the POC on your head's PC

1. Install the driver and plug in the scanner.
2. Copy `dist\` to the PC and edit the `.config` file.
3. Double-click `run_web.bat`. The console window shows `[ OK ] Scanner ready` and `[ OK ] Oracle OK`.
4. The browser opens at `http://localhost:8035/`.
5. Enter the PC No and click **Verify** (or press Enter). The light blinks, then glows with a 10 s countdown. Place the finger and see the result.

Exit codes for `--pcno` (useful for scripts):
`0` VERIFIED, `1` NOT_MATCHED, `2` TIMEOUT, `3` NOT_ENROLLED, `4` DEVICE_ERROR, `5` DB_ERROR, `6` INVALID_PCNO, `9` config/startup error.

## Web integration for the transaction application

Any page of the transaction app can call the local service:

```js
const r = await fetch('http://localhost:8035/api/verify', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ pcno: '123456' })
}).then(x => x.json());
// r = { verified, status, pcno, message, score, threshold, quality, templatesChecked, ... }
if (r.verified) { /* allow the transaction */ }
```

* `GET /api/status` reports whether the scanner and the database are reachable. Use it to show "scanner not connected" before the user starts.
* Calls from an `https://` intranet page to `http://localhost` are allowed by Chrome, Edge, and Firefox. The service also sends the `Access-Control-Allow-Private-Network` header that Chrome/Edge require.

### ⚠️ Before production: move matching to the server

In this POC the user's PC does the matching and reports the result. That is fine for a demonstration, but for real transactions **the server must not trust a "verified: true" coming from the client PC**, because a user could fake that localhost response. The production flow should be:

1. The local service only **captures** and returns the live ISO template. (The older `MfaScannerAgent` project on the `main` branch has a `/mfs100/capture` endpoint that can be reused for this.)
2. The browser posts that template together with the transaction to **your web server**.
3. The **server** reads the enrolled templates from Oracle and matches them, then approves the transaction. It should also log to an audit table and add replay protection (a one-time nonce per transaction).

Check with Mantra whether `MatchISO` works on a server **without a scanner attached**. Some SDK builds only match while a device is initialised. If yours does, use a server-side matcher such as Mantra's server/matcher SDK or another ISO 19794-2 matcher.

## Automating rollout to users

* **Per user, no admin:** run `install.bat` from the `dist` folder (or from a network share). It copies the program to `%LOCALAPPDATA%\FingerprintVerifyPOC`, starts it minimised at every logon (`--no-browser`), and adds a desktop link to the page. `uninstall.bat` removes it.
* **Company-wide:** have IT push the same folder with SCCM/Intune or a GPO logon script that runs `install.bat`, and push the Mantra driver separately (it needs admin). Put one shared `.config` on the share so every PC uses the same view and read-only DB user.
* **Production hardening (after the POC):**
  * Run it as a Windows Service or a tray app.
  * Restrict `AllowedOrigins` to the transaction site.
  * Move matching to the server as described above, so the DB password no longer lives on user PCs.
  * Sign the exe.
