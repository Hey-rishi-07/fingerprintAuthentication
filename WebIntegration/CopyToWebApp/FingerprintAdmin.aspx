<%@ Page Language="C#" EnableViewState="false" %>
<%--
  Admin page: enable / disable fingerprint authentication per PC No, and see recent attempts.
  Only logged-in users listed in FpMfa.AdminUsers (Web.config) can open it. No code-behind needed.
--%>
<%
    if (!FingerprintMfa.MfaLogin.IsAdmin(Context))
    {
        Response.StatusCode = 403;
        Response.Write("<h2 style='font-family:Segoe UI,Arial'>Not authorised</h2><p style='font-family:Segoe UI,Arial'>Log in with an administrator PC No listed in FpMfa.AdminUsers.</p>");
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
<title>Fingerprint Authentication Admin</title>
<style>
body{margin:0;background:#f3f5f9;color:#1c2430;font:15px/1.45 "Segoe UI",Arial,sans-serif}
.wrap{max-width:1000px;margin:24px auto;padding:0 16px}
h1{font-size:22px;margin:0 0 4px}.sub{color:#5d6b7e;margin:0 0 18px}
.card{background:#fff;border:1px solid #dbe1ea;border-radius:12px;padding:18px 20px;margin-bottom:18px}
h2{font-size:16px;margin:0 0 12px}
input,select{font-size:15px;padding:8px 10px;border:1px solid #c9d3e2;border-radius:7px}
button{font-size:14px;font-weight:600;padding:8px 14px;border:0;border-radius:7px;background:#1f5fbf;color:#fff;cursor:pointer}
button.off{background:#fff;color:#c62f2f;border:1px solid #e3b4b4}button.on{background:#fff;color:#1e8e4a;border:1px solid #a9d9bb}
.row{display:flex;flex-wrap:wrap;gap:8px;align-items:center}
table{width:100%;border-collapse:collapse;font-size:14px}th,td{text-align:left;padding:8px 6px;border-bottom:1px solid #e5e9f0;vertical-align:top}
th{color:#5d6b7e;font-size:12.5px;font-weight:600}
.pill{display:inline-block;padding:2px 9px;border-radius:99px;font-size:12.5px;font-weight:600}
.pill.y{background:#e5f6ec;color:#1e8e4a}.pill.n{background:#eef1f6;color:#5d6b7e}.pill.w{background:#fff6e5;color:#8a5a00}
.r-VERIFIED,.r-OK{color:#1e8e4a;font-weight:600}.r-NOT_MATCHED,.r-BLOCKED,.r-ERROR{color:#c62f2f;font-weight:600}
#note{margin:10px 0 0;font-weight:600}
.scroll{overflow-x:auto}
</style>
</head>
<body>
<div class="wrap">
  <h1>Fingerprint authentication</h1>
  <p class="sub" id="mode">Turn fingerprint verification after LDAP login on or off per PC No.</p>

  <div class="card">
    <h2>Enable / disable for a PC No</h2>
    <div class="row">
      <input id="newPcno" placeholder="PC No" maxlength="30" style="width:140px" />
      <input id="newRemarks" placeholder="Remarks (optional)" maxlength="200" style="flex:1;min-width:180px" />
      <button type="button" id="enableBtn">Enable</button>
      <button type="button" id="disableBtn" class="off">Disable</button>
    </div>
    <div id="note"></div>
  </div>

  <div class="card">
    <h2>Users</h2>
    <div class="row" style="margin-bottom:10px">
      <input id="search" placeholder="Search PC No" style="width:200px" />
      <button type="button" id="searchBtn">Search</button>
    </div>
    <div class="scroll"><table>
      <thead><tr><th>PC No</th><th>Fingerprint login</th><th>Enrolled fingers</th><th>Remarks</th><th>Changed</th><th></th></tr></thead>
      <tbody id="users"><tr><td colspan="6">Loading...</td></tr></tbody>
    </table></div>
  </div>

  <div class="card">
    <h2>Recent activity</h2>
    <div class="row" style="margin-bottom:10px">
      <input id="auditPcno" placeholder="Filter by PC No" style="width:200px" />
      <button type="button" id="auditBtn">Show</button>
    </div>
    <div class="scroll"><table>
      <thead><tr><th>Time</th><th>PC No</th><th>Event</th><th>Result</th><th>Score</th><th>IP</th><th>App</th><th>Details</th></tr></thead>
      <tbody id="audit"></tbody>
    </table></div>
  </div>
</div>

<script type="text/javascript">
(function () {
  var HANDLER = '<%= ResolveUrl("~/FingerprintMfa.ashx") %>';
  function $(id) { return document.getElementById(id); }
  function text(t) { return document.createTextNode(t == null ? '' : String(t)); }
  function cell(tr, content, cls) { var td = document.createElement('td'); if (cls) td.className = cls; if (content && content.nodeType) td.appendChild(content); else td.appendChild(text(content)); tr.appendChild(td); return td; }
  function pill(cls, t) { var s = document.createElement('span'); s.className = 'pill ' + cls; s.appendChild(text(t)); return s; }

  function call(action, query, body, done) {
    var x = new XMLHttpRequest();
    x.open(body ? 'POST' : 'GET', HANDLER + '?action=' + action + (query || '') + '&_=' + new Date().getTime(), true);
    x.setRequestHeader('Content-Type', 'application/json');
    x.setRequestHeader('X-FpMfa', '1');
    x.onload = function () { var d = null; try { d = JSON.parse(x.responseText); } catch (e) { } done(d || { ok: false, message: 'Server error' }); };
    x.onerror = function () { done({ ok: false, message: 'Server not reachable' }); };
    x.send(body ? JSON.stringify(body) : null);
  }

  function note(ok, t) { var n = $('note'); n.style.color = ok ? '#1e8e4a' : '#c62f2f'; n.innerHTML = ''; n.appendChild(text(t)); }

  function setUser(pcno, enabled, remarks) {
    call('admin-set', '', { pcno: pcno, enabled: enabled ? 'true' : 'false', remarks: remarks || '' }, function (r) {
      note(r.ok, r.message); loadUsers(); loadAudit();
    });
  }

  function loadUsers() {
    call('admin-users', '&q=' + encodeURIComponent($('search').value), null, function (r) {
      var tb = $('users'); tb.innerHTML = '';
      if (!r.ok) { var tr0 = document.createElement('tr'); cell(tr0, r.message).colSpan = 6; tb.appendChild(tr0); return; }
      $('mode').innerHTML = '';
      $('mode').appendChild(text(!r.enabled ? 'Fingerprint authentication is switched OFF for this application (FpMfa.Enabled=false).'
        : r.requireForAll ? 'Mode: ALL users must give a fingerprint (FpMfa.RequireForAllUsers=true). Disabled users below are still asked.'
        : 'Mode: only users ENABLED below are asked for a fingerprint after LDAP login.'));
      if (!r.users.length) { var tr1 = document.createElement('tr'); cell(tr1, 'No users yet. Enable a PC No above.').colSpan = 6; tb.appendChild(tr1); return; }
      for (var i = 0; i < r.users.length; i++) (function (u) {
        var tr = document.createElement('tr');
        cell(tr, u.pcno);
        cell(tr, pill(u.enabled ? 'y' : 'n', u.enabled ? 'Enabled' : 'Disabled'));
        cell(tr, u.fingers > 0 ? String(u.fingers) : pill('w', '0 - not enrolled'));
        cell(tr, u.remarks);
        cell(tr, u.updatedOn + (u.updatedBy ? ' by ' + u.updatedBy : ''));
        var b = document.createElement('button'); b.type = 'button';
        b.className = u.enabled ? 'off' : 'on'; b.appendChild(text(u.enabled ? 'Disable' : 'Enable'));
        b.onclick = function () { setUser(u.pcno, !u.enabled, null); };
        cell(tr, b);
        tb.appendChild(tr);
      })(r.users[i]);
    });
  }

  function loadAudit() {
    call('admin-audit', '&pcno=' + encodeURIComponent($('auditPcno').value), null, function (r) {
      var tb = $('audit'); tb.innerHTML = '';
      if (!r.ok) return;
      for (var i = 0; i < r.rows.length; i++) {
        var a = r.rows[i], tr = document.createElement('tr');
        cell(tr, a.time); cell(tr, a.pcno); cell(tr, a.evt); cell(tr, a.result, 'r-' + a.result);
        cell(tr, a.score || ''); cell(tr, a.ip); cell(tr, a.app); cell(tr, a.message);
        tb.appendChild(tr);
      }
    });
  }

  $('enableBtn').onclick = function () { var p = $('newPcno').value.replace(/^\s+|\s+$/g, ''); if (p) setUser(p, true, $('newRemarks').value); };
  $('disableBtn').onclick = function () { var p = $('newPcno').value.replace(/^\s+|\s+$/g, ''); if (p) setUser(p, false, $('newRemarks').value); };
  $('searchBtn').onclick = loadUsers;
  $('search').onkeydown = function (e) { if ((e || window.event).keyCode === 13) loadUsers(); };
  $('auditBtn').onclick = loadAudit;
  loadUsers(); loadAudit();
})();
</script>
</body>
</html>
