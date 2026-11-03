using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.SessionState;

namespace FingerprintMfa
{
    /// <summary>
    /// JSON endpoints used by FingerprintVerify.aspx and FingerprintAdmin.aspx
    /// (FingerprintMfa.ashx?action=...). POST calls must send the header "X-FpMfa: 1": a custom
    /// header cannot be sent cross-site without CORS approval, so other websites cannot call this.
    /// </summary>
    public class FingerprintHandler : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable { get { return true; } }

        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 };

        public void ProcessRequest(HttpContext ctx)
        {
            ctx.Response.ContentType = "application/json";
            ctx.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            string action = (ctx.Request.QueryString["action"] ?? "").ToLowerInvariant();
            bool post = ctx.Request.HttpMethod == "POST";

            if (post && ctx.Request.Headers["X-FpMfa"] != "1") { Write(ctx, new { ok = false, message = "Bad request" }, 400); return; }

            try
            {
                switch (action)
                {
                    case "start": Start(ctx); break;
                    case "verify": if (post) Verify(ctx); else NotFound(ctx); break;
                    case "cancel": if (post) { MfaLogin.Cancel(ctx); Write(ctx, new { ok = true, redirect = Url(MfaConfig.Current.LoginUrl) }); } else NotFound(ctx); break;
                    case "admin-users": if (Admin(ctx)) AdminUsers(ctx); break;
                    case "admin-set": if (post && Admin(ctx)) AdminSet(ctx); else if (!post) NotFound(ctx); break;
                    case "admin-audit": if (Admin(ctx)) AdminAudit(ctx); break;
                    default: NotFound(ctx); break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("FingerprintMfa handler: " + ex);
                Write(ctx, new { ok = false, message = "Server error: " + ex.Message }, 500);
            }
        }

        // ------------------------------------------------------------------ login step

        private void Start(HttpContext ctx)
        {
            MfaConfig c = MfaConfig.Current;
            MfaLogin.Pending p = MfaLogin.GetPending(ctx);
            if (p == null) { Write(ctx, new { ok = false, message = "Session expired. Please log in again.", redirect = Url(c.LoginUrl) }); return; }
            p.Challenge = MfaLogin.NewChallenge();
            Write(ctx, new
            {
                ok = true,
                pcno = p.Pcno,
                agentUrl = c.AgentUrl,
                downloadUrl = string.IsNullOrEmpty(c.AgentDownloadUrl) ? "" : Url(c.AgentDownloadUrl),
                timeout = c.CaptureTimeoutSeconds,
                attemptsLeft = c.MaxAttempts - p.Attempts,
                challenge = p.Challenge
            });
        }

        private void Verify(HttpContext ctx)
        {
            MfaConfig c = MfaConfig.Current;
            MfaLogin.Pending p = MfaLogin.GetPending(ctx);
            if (p == null) { Write(ctx, new { ok = false, message = "Session expired. Please log in again.", redirect = Url(c.LoginUrl) }); return; }

            Dictionary<string, object> body = ReadBody(ctx);
            string challenge = Str(body, "challenge");
            if (string.IsNullOrEmpty(challenge) || challenge != p.Challenge)
            {
                Write(ctx, new { ok = false, message = "Please try again.", retry = true });
                return;
            }
            p.Challenge = MfaLogin.NewChallenge();   // one use only

            byte[] iso = B64(Str(body, "iso"));
            byte[] ansi = B64(Str(body, "ansi"));
            int quality;
            int.TryParse(Str(body, "quality"), out quality);
            if ((iso == null || iso.Length == 0) && (ansi == null || ansi.Length == 0))
            {
                Write(ctx, new { ok = false, message = "No fingerprint received.", retry = true, challenge = p.Challenge });
                return;
            }

            p.Attempts++;
            MfaVerifier.Result r = MfaVerifier.Verify(p.Pcno, iso, ansi, quality);
            if (r.Verified)
            {
                string redirect = MfaLogin.Complete(ctx, p);
                Write(ctx, new { ok = true, verified = true, message = r.Message, redirect = redirect });
                return;
            }

            int left = c.MaxAttempts - p.Attempts;
            bool hardFail = r.Status == "NOT_ENROLLED" || r.Status == "ERROR";
            if (left <= 0 || hardFail)
            {
                MfaLogin.Cancel(ctx);
                new MfaRepository(c).Audit(p.Pcno, "LOGIN", "BLOCKED", r.Score, quality, MfaLogin.ClientIp(),
                                           hardFail ? r.Status : "Too many failed attempts");
                Write(ctx, new
                {
                    ok = true, verified = false, status = r.Status,
                    message = hardFail ? r.Message : r.Message + " Too many attempts - please log in again.",
                    redirect = Url(c.LoginUrl), locked = true
                });
                return;
            }
            Write(ctx, new { ok = true, verified = false, status = r.Status, message = r.Message, attemptsLeft = left, challenge = p.Challenge });
        }

        // ------------------------------------------------------------------ admin

        private bool Admin(HttpContext ctx)
        {
            if (MfaLogin.IsAdmin(ctx)) return true;
            Write(ctx, new { ok = false, message = "Not authorised" }, 403);
            return false;
        }

        private void AdminUsers(HttpContext ctx)
        {
            var users = new MfaRepository(MfaConfig.Current).ListUsers(ctx.Request.QueryString["q"]);
            Write(ctx, new
            {
                ok = true,
                requireForAll = MfaConfig.Current.RequireForAllUsers,
                enabled = MfaConfig.Current.Enabled,
                users = users.Select(u => new
                {
                    pcno = u.Pcno, enabled = u.Enabled, remarks = u.Remarks, fingers = u.Fingers,
                    updatedBy = u.UpdatedBy, updatedOn = u.UpdatedOn.HasValue ? u.UpdatedOn.Value.ToString("yyyy-MM-dd HH:mm") : ""
                }).ToList()
            });
        }

        private void AdminSet(HttpContext ctx)
        {
            Dictionary<string, object> body = ReadBody(ctx);
            string pcno = (Str(body, "pcno") ?? "").Trim();
            if (pcno.Length == 0 || pcno.Length > 30) { Write(ctx, new { ok = false, message = "Enter a valid PC No" }); return; }
            bool enabled = string.Equals(Str(body, "enabled"), "true", StringComparison.OrdinalIgnoreCase);
            string remarks = Str(body, "remarks");
            if (remarks != null && remarks.Length > 200) remarks = remarks.Substring(0, 200);

            var repo = new MfaRepository(MfaConfig.Current);
            string admin = MfaLogin.CurrentUser(ctx);
            repo.SetUser(pcno, enabled, remarks, admin);
            repo.Audit(pcno, enabled ? "ADMIN_ENABLE" : "ADMIN_DISABLE", "OK", 0, 0, MfaLogin.ClientIp(), "by " + admin);
            int fingers = repo.CountTemplates(pcno);
            Write(ctx, new
            {
                ok = true,
                message = "Fingerprint authentication " + (enabled ? "ENABLED" : "DISABLED") + " for " + pcno +
                          (enabled && fingers == 0 ? ". Warning: no fingerprint is enrolled for this PC No - the user will not be able to log in." : ".")
            });
        }

        private void AdminAudit(HttpContext ctx)
        {
            var rows = new MfaRepository(MfaConfig.Current).RecentAudit(ctx.Request.QueryString["pcno"], 100);
            Write(ctx, new
            {
                ok = true,
                rows = rows.Select(a => new
                {
                    time = a.Time.ToString("yyyy-MM-dd HH:mm:ss"), pcno = a.Pcno, evt = a.Event, result = a.Result,
                    score = a.Score, ip = a.ClientIp, app = a.App, message = a.Message
                }).ToList()
            });
        }

        // ------------------------------------------------------------------ helpers

        private static Dictionary<string, object> ReadBody(HttpContext ctx)
        {
            if (ctx.Request.ContentLength > 200 * 1024) return new Dictionary<string, object>();
            string text;
            using (var rd = new StreamReader(ctx.Request.InputStream)) text = rd.ReadToEnd();
            try { return Json.Deserialize<Dictionary<string, object>>(text) ?? new Dictionary<string, object>(); }
            catch { return new Dictionary<string, object>(); }
        }

        private static string Str(Dictionary<string, object> d, string k)
        {
            object v;
            return d.TryGetValue(k, out v) && v != null ? Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) : null;
        }

        private static byte[] B64(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length > 40000) return null;
            try { return Convert.FromBase64String(s); } catch { return null; }
        }

        private static string Url(string appRelative)
        {
            return appRelative.StartsWith("~") ? VirtualPathUtility.ToAbsolute(appRelative) : appRelative;
        }

        private static void NotFound(HttpContext ctx) { Write(ctx, new { ok = false, message = "Unknown action" }, 404); }

        private static void Write(HttpContext ctx, object o, int status = 200)
        {
            ctx.Response.StatusCode = status;
            ctx.Response.Write(Json.Serialize(o));
        }
    }
}
