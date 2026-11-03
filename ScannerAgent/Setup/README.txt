Fingerprint Scanner Agent - setup
=================================

What it is
  A small background program (no window) that lets your intranet web
  applications use the Mantra MFS100 fingerprint scanner. It only captures the
  finger; it has NO database access. Verification happens on the web server.

Install (once per PC)
  1. Plug in the scanner.
  2. Double-click Setup.bat and click "Yes" when Windows asks for admin rights.
  3. Press Y to install the Mantra driver (N if it is already installed).
  4. At the end it shows {"agent":"FpScannerAgent",...,"scannerReady":true}.

After that the agent starts automatically, hidden, every time any user logs on.

Check it
  Open http://localhost:8036/status in the browser.
  Log file: %LOCALAPPDATA%\FpScannerAgent\agent.log

If the web page says "Fingerprint service not running"
  Click "Start service" on that page, or log off and on again.

Problems
  - scannerReady:false, error -1 : another program holds the scanner (Mantra
    RD Service / AVDM, Mantra test tool). Close it, replug the scanner.
  - Remove: Uninstall.bat

For IT: before distributing, set AllowedOrigins in Agent\FpScannerAgent.exe.config
to your web application address(es), e.g. https://txn.company.local
