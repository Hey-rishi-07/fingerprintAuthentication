# Fingerprint Authentication (Mantra MFS100 + Oracle)

Open **`FingerprintAuthentication.sln`** in Visual Studio. Every project also has a `build.bat` that works without Visual Studio.

| Folder | What it is |
|---|---|
| [`WebIntegration/`](WebIntegration/README.md) | **Production:** fingerprint step after LDAP login for ASP.NET WebForms apps (server-side verification, admin page, audit). |
| [`ScannerAgent/`](ScannerAgent/README.md) | **Production:** background agent plus a one-time `Setup.bat` for user PCs. Capture only, no secrets. |
| [`FingerprintVerifyPOC/`](FingerprintVerifyPOC/README.md) | Standalone POC and test tool: PC No, verify, enrol into a test table, diagnostics. |
| `Lib/` | Mantra SDK (x86 and x64 native) and Oracle managed driver, shared by all projects. |
| `Driver/` | Mantra MFS100 driver installer. |
