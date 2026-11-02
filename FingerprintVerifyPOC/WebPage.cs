namespace FingerprintVerifyPOC
{
    /// <summary>The built-in verification page served at http://localhost:{port}/ (kept inline so the exe is self-contained).</summary>
    internal static class WebPage
    {
        public static string Html(Settings s)
        {
            return Template.Replace("{{TIMEOUT}}", s.CaptureTimeoutSeconds.ToString());
        }

        private const string Template = @"<!doctype html>
<html lang=""en""><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<title>Fingerprint Verification</title>
<style>
:root{--bg:#f3f5f9;--card:#fff;--ink:#1c2430;--muted:#5d6b7e;--line:#dbe1ea;--brand:#1f5fbf;--ok:#1e8e4a;--bad:#c62f2f;--warn:#b7791f}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--ink);font:16px/1.45 Segoe UI,Arial,sans-serif;display:flex;min-height:100vh;align-items:center;justify-content:center;padding:16px}
.card{background:var(--card);border:1px solid var(--line);border-radius:14px;box-shadow:0 8px 28px rgba(20,40,80,.08);width:100%;max-width:440px;padding:28px}
h1{font-size:20px;margin:0 0 4px}.sub{color:var(--muted);font-size:14px;margin:0 0 22px}
label{font-size:13px;font-weight:600;color:var(--muted);display:block;margin-bottom:6px}
.row{display:flex;gap:8px}input{flex:1;font-size:18px;padding:11px 12px;border:1px solid var(--line);border-radius:8px;letter-spacing:.5px}
input:focus{outline:2px solid var(--brand);border-color:transparent}
button{font-size:16px;font-weight:600;padding:11px 18px;border:0;border-radius:8px;background:var(--brand);color:#fff;cursor:pointer}
button:disabled{opacity:.5;cursor:default}
.sensor{margin:26px auto 10px;width:130px;height:130px;border-radius:50%;display:flex;align-items:center;justify-content:center;background:#eef1f6;border:3px solid var(--line);transition:.2s}
.sensor svg{width:70px;height:70px;stroke:#9aa7b8}
.sensor.blink{animation:blink .5s steps(2,start) infinite}
.sensor.on{background:#ffe9e9;border-color:#e04848;box-shadow:0 0 28px rgba(224,72,72,.55)}.sensor.on svg{stroke:#d33}
.sensor.ok{background:#e5f6ec;border-color:var(--ok)}.sensor.ok svg{stroke:var(--ok)}
.sensor.bad{background:#fdeaea;border-color:var(--bad)}.sensor.bad svg{stroke:var(--bad)}
@keyframes blink{to{background:#ffe9e9;border-color:#e04848;box-shadow:0 0 28px rgba(224,72,72,.55)}}
.count{text-align:center;font-size:32px;font-weight:700;height:40px;color:#d33}
.msg{text-align:center;min-height:48px;font-size:15px;color:var(--muted)}
.result{border-radius:10px;padding:12px 14px;margin-top:8px;font-weight:600;text-align:center;display:none}
.result.ok{display:block;background:#e5f6ec;color:var(--ok)}.result.bad{display:block;background:#fdeaea;color:var(--bad)}.result.warn{display:block;background:#fff6e5;color:var(--warn)}
.detail{font-weight:400;font-size:12.5px;color:var(--muted);margin-top:4px}
.status{display:flex;gap:14px;justify-content:center;font-size:12.5px;color:var(--muted);margin-top:18px}
.dot{display:inline-block;width:8px;height:8px;border-radius:50%;background:#aab;margin-right:5px;vertical-align:1px}.dot.g{background:var(--ok)}.dot.r{background:var(--bad)}
</style></head><body>
<div class=""card"">
  <h1>Fingerprint Verification</h1>
  <p class=""sub"">Enter your PC No, then place your finger when the scanner light glows.</p>
  <label for=""pcno"">PC No</label>
  <div class=""row""><input id=""pcno"" autocomplete=""off"" autofocus maxlength=""30"" placeholder=""e.g. 123456"">
  <button id=""go"">Verify</button></div>
  <div id=""sensor"" class=""sensor""><svg viewBox=""0 0 24 24"" fill=""none"" stroke-width=""1.6"" stroke-linecap=""round""><path d=""M12 11c0 3.5-1 6.5-2.5 9""/><path d=""M8.5 4.8A7 7 0 0 1 19 11c0 1.6-.1 3.2-.4 4.7""/><path d=""M5 9a7 7 0 0 1 1.6-2.6""/><path d=""M5 13c.2 2.3-.2 4.3-1 6""/><path d=""M15.5 11.5c0 3-.6 6-1.8 8.5""/><path d=""M8.6 11a3.4 3.4 0 0 1 6.9 0""/><path d=""M8.5 14.5c-.2 2-.8 3.8-1.7 5.3""/></svg></div>
  <div id=""count"" class=""count""></div>
  <div id=""msg"" class=""msg"">Ready</div>
  <div id=""result"" class=""result""></div>
  <div class=""status""><span><i id=""dDev"" class=""dot""></i><span id=""tDev"">Scanner</span></span><span><i id=""dDb"" class=""dot""></i><span id=""tDb"">Database</span></span></div>
</div>
<script>
var TIMEOUT={{TIMEOUT}}, blinkMs=1500, timer=null;
var $=function(id){return document.getElementById(id)};
function sensor(c){$('sensor').className='sensor '+(c||'')}
function setMsg(t){$('msg').textContent=t}
function result(cls,title,detail){var r=$('result');r.className='result '+cls;r.innerHTML='';var a=document.createElement('div');a.textContent=title;r.appendChild(a);
 if(detail){var d=document.createElement('div');d.className='detail';d.textContent=detail;r.appendChild(d)}}
function stopTimer(){if(timer){clearInterval(timer);timer=null}$('count').textContent=''}
function status(){fetch('/api/status').then(function(r){return r.json()}).then(function(s){
  TIMEOUT=s.timeoutSeconds;blinkMs=s.blinkMs;
  $('dDev').className='dot '+(s.scannerReady?'g':'r');$('tDev').textContent=s.scannerReady?'Scanner ready':'Scanner not connected';$('tDev').title=s.scannerMessage;
  $('dDb').className='dot '+(s.databaseReady?'g':'r');$('tDb').textContent=s.databaseReady?'Database OK':'Database error';$('tDb').title=s.databaseMessage;
 }).catch(function(){})}
function verify(){
  var pcno=$('pcno').value.trim(); if(!pcno){$('pcno').focus();return}
  $('go').disabled=true;$('pcno').disabled=true;$('result').className='result';
  sensor('blink');setMsg('Get ready...');
  var left=TIMEOUT;
  setTimeout(function(){ if(!$('go').disabled)return; sensor('on');setMsg('Place your finger on the scanner');$('count').textContent=left;
    timer=setInterval(function(){left--;$('count').textContent=left>0?left:'';if(left<=0)stopTimer()},1000)},blinkMs);
  fetch('/api/verify',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({pcno:pcno})})
   .then(function(r){return r.json()}).then(function(r){
     stopTimer();
     var d='Score '+r.score+' / '+r.threshold+(r.quality?'  |  Quality '+r.quality:'')+(r.templatesChecked?'  |  Fingers checked '+r.templatesChecked:'');
     if(r.verified){sensor('ok');setMsg('PC No '+r.pcno);result('ok','✔ Authentication successful',d)}
     else if(r.status==='NOT_MATCHED'){sensor('bad');setMsg(r.message);result('bad','✖ Authentication failed',d)}
     else {sensor('');setMsg(r.message);result('warn',r.status.replace('_',' '),'')}
   }).catch(function(e){stopTimer();sensor('');result('warn','Service error',String(e))})
   .then(function(){$('go').disabled=false;$('pcno').disabled=false;$('pcno').select();status()});
}
$('go').onclick=verify;$('pcno').onkeydown=function(e){if(e.key==='Enter')verify()};
status();
</script></body></html>";
    }
}
