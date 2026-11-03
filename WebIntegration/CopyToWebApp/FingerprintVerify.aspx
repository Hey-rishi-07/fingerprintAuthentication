<%@ Page Language="C#" EnableViewState="false" %>
<%--
  Fingerprint step after LDAP login. No code-behind needed: copy this file into the web application root.
  The user arrives here only via FingerprintMfa.MfaLogin.BeginLogin(...) in your Login page.
--%>
<%
    if (!FingerprintMfa.MfaLogin.HasPending(Context))
    {
        Response.Redirect(VirtualPathUtility.ToAbsolute(FingerprintMfa.MfaConfig.Current.LoginUrl), false);
        Context.ApplicationInstance.CompleteRequest();
        return;
    }
%>
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8" />
<meta http-equiv="X-UA-Compatible" content="IE=edge" />
<meta name="viewport" content="width=device-width,initial-scale=1" />
<title>Fingerprint Verification</title>
<style>
body{margin:0;background:#f3f5f9;color:#1c2430;font:16px/1.45 "Segoe UI",Arial,sans-serif}
.wrap{max-width:440px;margin:48px auto;padding:0 16px}
.card{background:#fff;border:1px solid #dbe1ea;border-radius:14px;box-shadow:0 8px 28px rgba(20,40,80,.08);padding:28px;text-align:center}
h1{font-size:20px;margin:0 0 4px}.sub{color:#5d6b7e;font-size:14px;margin:0 0 8px}
.sensor{margin:22px auto 10px;width:130px;height:130px;border-radius:50%;background:#eef1f6;border:3px solid #dbe1ea;line-height:130px;font-size:56px;color:#9aa7b8}
.sensor.on{background:#ffe9e9;border-color:#e04848;color:#d33;box-shadow:0 0 28px rgba(224,72,72,.55)}
.sensor.blink{animation:blink .5s steps(2,start) infinite}
@keyframes blink{to{background:#ffe9e9;border-color:#e04848;color:#d33}}
.sensor.ok{background:#e5f6ec;border-color:#1e8e4a;color:#1e8e4a}.sensor.bad{background:#fdeaea;border-color:#c62f2f;color:#c62f2f}
.count{font-size:30px;font-weight:700;height:38px;color:#d33}
.msg{min-height:44px;color:#5d6b7e}
.alert{border-radius:10px;padding:12px;margin:10px 0;font-weight:600;display:none}
.alert.ok{display:block;background:#e5f6ec;color:#1e8e4a}.alert.bad{display:block;background:#fdeaea;color:#c62f2f}.alert.warn{display:block;background:#fff6e5;color:#8a5a00}
button,.btn{display:inline-block;font-size:15px;font-weight:600;padding:10px 18px;border:0;border-radius:8px;background:#1f5fbf;color:#fff;cursor:pointer;text-decoration:none;margin:4px}
.btn.light{background:#fff;color:#1f5fbf;border:1px solid #c9d3e2}
button:disabled{opacity:.5;cursor:default}
#agentHelp{display:none}
.small{font-size:13px;color:#5d6b7e;margin-top:14px}
</style>
</head>
<body>
<div class="wrap"><div class="card">
  <h1>Fingerprint verification</h1>
  <p class="sub">PC No <b id="pcno">...</b> &mdash; place your registered finger on the scanner.</p>

  <div id="sensor" class="sensor">&#9673;</div>
  <div id="count" class="count"></div>
  <div id="msg" class="msg">Checking the fingerprint scanner...</div>
  <div id="alert" class="alert"></div>

  <div id="agentHelp">
    <p>The fingerprint service is not running on this computer.</p>
    <a class="btn" href="fpscanneragent://start">Start service</a>
    <a class="btn light" id="installLink" href="#" target="_blank">Install (one time)</a>
    <div class="small">After installing, this page continues automatically.</div>
  </div>

  <div>
    <button id="scanBtn" type="button" disabled="disabled">Scan fingerprint</button>
  </div>
  <div class="small"><a href="#" id="cancelLink">Cancel and go back to login</a></div>
</div></div>

<script type="text/javascript">
(function () {
  var HANDLER = '<%= ResolveUrl("~/FingerprintMfa.ashx") %>';
  var cfg = null, timer = null, poll = null, busy = false;

  function $(id) { return document.getElementById(id); }
  function msg(t) { $('msg').innerHTML = ''; $('msg').appendChild(document.createTextNode(t)); }
  function alertBox(cls, t) { var a = $('alert'); a.className = 'alert ' + (cls || ''); a.innerHTML = ''; if (t) a.appendChild(document.createTextNode(t)); }
  function sensor(c) { $('sensor').className = 'sensor ' + (c || ''); }
  function stopTimer() { if (timer) { clearInterval(timer); timer = null; } $('count').innerHTML = ''; }

  // XMLHttpRequest (works in IE11 / Edge IE-mode as well)
  function ajax(method, url, body, headers, timeoutMs, done) {
    var x = new XMLHttpRequest(), finished = false;
    x.open(method, url, true);
    x.setRequestHeader('Content-Type', 'application/json');
    for (var h in headers) if (headers.hasOwnProperty(h)) x.setRequestHeader(h, headers[h]);
    if (timeoutMs) x.timeout = timeoutMs;
    function end(err, data) { if (finished) return; finished = true; done(err, data); }
    x.onload = function () { var d = null; try { d = JSON.parse(x.responseText); } catch (e) { } end(d ? null : 'Bad response', d); };
    x.onerror = function () { end('unreachable'); };
    x.ontimeout = function () { end('timeout'); };
    x.send(body ? JSON.stringify(body) : null);
  }
  function server(action, body, done) {
    ajax(body ? 'POST' : 'GET', HANDLER + '?action=' + action + '&_=' + new Date().getTime(), body, { 'X-FpMfa': '1' }, 30000, done);
  }

  function start() {
    server('start', null, function (err, d) {
      if (err || !d) { msg('Server not reachable. Please reload the page.'); return; }
      if (!d.ok) { alertBox('warn', d.message); if (d.redirect) setTimeout(function () { location.href = d.redirect; }, 1500); return; }
      cfg = d;
      $('pcno').innerHTML = ''; $('pcno').appendChild(document.createTextNode(d.pcno));
      if (d.downloadUrl) $('installLink').href = d.downloadUrl; else $('installLink').style.display = 'none';
      checkAgent(true);
    });
  }

  // Is the Scanner Agent running on this PC and is the scanner plugged in?
  function checkAgent(autoScan) {
    ajax('GET', cfg.agentUrl + '/status', null, {}, 3000, function (err, s) {
      if (err || !s) {
        $('agentHelp').style.display = 'block'; $('scanBtn').disabled = true;
        msg('Waiting for the fingerprint service...');
        schedulePoll(); return;
      }
      $('agentHelp').style.display = 'none';
      if (!s.scannerReady) {
        $('scanBtn').disabled = true;
        msg('Please connect the fingerprint scanner (USB).');
        schedulePoll(); return;
      }
      if (poll) { clearTimeout(poll); poll = null; }
      $('scanBtn').disabled = false;
      msg('Scanner ready.');
      if (autoScan) scan();
    });
  }
  function schedulePoll() { if (!poll) poll = setTimeout(function () { poll = null; checkAgent(true); }, 3000); }

  function scan() {
    if (busy) return;
    busy = true; $('scanBtn').disabled = true; alertBox('', '');
    sensor('blink'); msg('Get ready...');
    var left = cfg.timeout;
    setTimeout(function () {
      if (!busy) return;
      sensor('on'); msg('Place your finger on the scanner'); $('count').innerHTML = left;
      timer = setInterval(function () { left--; $('count').innerHTML = left > 0 ? left : ''; if (left <= 0) stopTimer(); }, 1000);
    }, 1500);

    ajax('POST', cfg.agentUrl + '/capture', { timeout: cfg.timeout, blink: true }, {}, (cfg.timeout + 15) * 1000, function (err, c) {
      stopTimer();
      if (err || !c) { done('bad', 'Fingerprint service stopped responding.'); checkAgent(false); return; }
      if (!c.ok) { done('', c.message || 'Capture failed.'); return; }
      sensor(''); msg('Verifying...');
      server('verify', { challenge: cfg.challenge, iso: c.iso, ansi: c.ansi, quality: c.quality }, function (err2, r) {
        if (err2 || !r) { done('bad', 'Server error. Please try again.'); return; }
        if (r.challenge) cfg.challenge = r.challenge;
        if (r.verified) {
          sensor('ok'); msg(''); alertBox('ok', '✔ ' + r.message);
          setTimeout(function () { location.href = r.redirect; }, 700);
          return;
        }
        if (r.locked || (!r.ok && r.redirect)) {
          sensor('bad'); msg(''); alertBox('bad', r.message);
          setTimeout(function () { location.href = r.redirect; }, 3500);
          return;
        }
        if (r.retry) { done('', r.message); return; }
        done('bad', '✖ ' + r.message + (r.attemptsLeft ? '  (' + r.attemptsLeft + ' attempt(s) left)' : ''));
      });
    });
  }

  function done(cls, text) {
    busy = false; stopTimer();
    sensor(cls === 'bad' ? 'bad' : '');
    msg('Press "Scan fingerprint" to try again.');
    alertBox(cls === 'bad' ? 'bad' : 'warn', text);
    $('scanBtn').disabled = false;
  }

  $('scanBtn').onclick = scan;
  $('cancelLink').onclick = function () {
    server('cancel', {}, function (e, r) { location.href = (r && r.redirect) || '<%= ResolveUrl(FingerprintMfa.MfaConfig.Current.LoginUrl) %>'; });
    return false;
  };
  start();
})();
</script>
</body>
</html>
