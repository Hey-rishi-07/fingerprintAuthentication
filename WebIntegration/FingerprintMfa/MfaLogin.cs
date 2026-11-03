using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Web;
using System.Web.Security;

namespace FingerprintMfa
{
    /// <summary>
    /// What your Login page uses.
    ///
    ///   // after LDAP succeeded:
    ///   if (FingerprintMfa.MfaLogin.BeginLogin(Context, pcno, Request.QueryString["ReturnUrl"]))
    ///       return;                       // user is sent to FingerprintVerify.aspx
    ///   // fingerprint not required for this user -> your normal login code
    ///
    /// After a successful fingerprint the user is signed in (FpMfa.SignInMode) and the
    /// <see cref="SignedIn"/> event is raised, so your own "after login" code can run.
    /// </summary>
    public static class MfaLogin
    {
        private const string PendingKey = "FpMfa.Pending";

        /// <summary>Raised after a successful fingerprint, before the redirect. Hook it in Global.asax Application_Start.</summary>
        public static event Action<HttpContext, string> SignedIn;

        internal class Pending
        {
            public string Pcno;
            public string ReturnUrl;
            public DateTime Expires;
            public int Attempts;
            public string Challenge;
        }

        /// <summary>Does this PC No have to give a fingerprint?</summary>
        public static bool IsRequired(string pcno)
        {
            MfaConfig c = MfaConfig.Current;
            if (!c.Enabled || string.IsNullOrWhiteSpace(pcno)) return false;
            var repo = new MfaRepository(c);
            try
            {
                bool required = c.RequireForAllUsers || repo.GetUserEnabled(pcno.Trim()) == true;
                if (required && c.AllowIfNotEnrolled && repo.CountTemplates(pcno.Trim()) == 0)
                {
                    repo.Audit(pcno, "LOGIN", "SKIPPED", 0, 0, ClientIp(), "Fingerprint required but not enrolled (AllowIfNotEnrolled=true)");
                    return false;
                }
                return required;
            }
            catch (Exception ex)
            {
                // database problem: fail closed - the user is asked for a fingerprint (and the page shows the error)
                System.Diagnostics.Trace.TraceError("FingerprintMfa.IsRequired: " + ex.Message);
                return true;
            }
        }

        /// <summary>
        /// Call right after the LDAP login succeeded. Returns true when the user was redirected to the
        /// fingerprint page (stop your login code), false when no fingerprint is needed (continue as before).
        /// IMPORTANT: do not mark the user as logged in before calling this.
        /// </summary>
        public static bool BeginLogin(HttpContext ctx, string pcno, string returnUrl)
        {
            if (!IsRequired(pcno)) return false;

            ctx.Session[PendingKey] = new Pending
            {
                Pcno = pcno.Trim(),
                ReturnUrl = SafeLocalUrl(returnUrl),
                Expires = DateTime.Now.AddMinutes(MfaConfig.Current.PendingMinutes),
                Attempts = 0,
                Challenge = NewChallenge()
            };
            ctx.Response.Redirect(MfaConfig.Current.VerifyUrl, false);
            ctx.ApplicationInstance.CompleteRequest();
            return true;
        }

        /// <summary>True while a user has passed LDAP and still has to give the fingerprint.</summary>
        public static bool HasPending(HttpContext ctx)
        {
            return GetPending(ctx) != null;
        }

        /// <summary>Cancels a pending fingerprint step (e.g. "Back to login").</summary>
        public static void Cancel(HttpContext ctx)
        {
            if (ctx.Session != null) ctx.Session.Remove(PendingKey);
        }

        /// <summary>The logged-in user's PC No (Forms identity or the session key).</summary>
        public static string CurrentUser(HttpContext ctx)
        {
            MfaConfig c = MfaConfig.Current;
            if (c.SignInMode.Equals("Session", StringComparison.OrdinalIgnoreCase))
                return ctx.Session != null ? Convert.ToString(ctx.Session[c.SessionUserKey]) : null;
            return ctx.User != null && ctx.User.Identity.IsAuthenticated ? ctx.User.Identity.Name : null;
        }

        /// <summary>Is the logged-in user listed in FpMfa.AdminUsers?</summary>
        public static bool IsAdmin(HttpContext ctx)
        {
            string user = CurrentUser(ctx);
            return !string.IsNullOrEmpty(user) &&
                   MfaConfig.Current.AdminUsers.Any(a => a.Equals(user, StringComparison.OrdinalIgnoreCase));
        }

        // ------------------------------------------------------------------ internal

        internal static Pending GetPending(HttpContext ctx)
        {
            if (ctx.Session == null) return null;
            var p = ctx.Session[PendingKey] as Pending;
            if (p == null) return null;
            if (DateTime.Now > p.Expires) { ctx.Session.Remove(PendingKey); return null; }
            return p;
        }

        internal static string Complete(HttpContext ctx, Pending p)
        {
            ctx.Session.Remove(PendingKey);
            MfaConfig c = MfaConfig.Current;
            if (c.SignInMode.Equals("Forms", StringComparison.OrdinalIgnoreCase))
                FormsAuthentication.SetAuthCookie(p.Pcno, false);
            else if (c.SignInMode.Equals("Session", StringComparison.OrdinalIgnoreCase))
                ctx.Session[c.SessionUserKey] = p.Pcno;

            Action<HttpContext, string> h = SignedIn;
            if (h != null) h(ctx, p.Pcno);

            return VirtualPathUtility.ToAbsolute(p.ReturnUrl ?? c.DefaultUrl);
        }

        internal static string NewChallenge()
        {
            var b = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
            return BitConverter.ToString(b).Replace("-", "");
        }

        internal static string ClientIp()
        {
            HttpContext ctx = HttpContext.Current;
            return ctx != null ? ctx.Request.UserHostAddress : null;
        }

        /// <summary>Only local paths are accepted as return URL (no open redirect).</summary>
        private static string SafeLocalUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            url = url.Trim();
            if (url.StartsWith("~/")) return url;
            if (url.StartsWith("/") && !url.StartsWith("//") && !url.StartsWith("/\\")) return url;
            return null;
        }
    }

    /// <summary>Server-side verification of a captured template against the user's enrolled fingers.</summary>
    internal static class MfaVerifier
    {
        public class Result
        {
            public bool Verified;
            public string Status;     // VERIFIED, NOT_MATCHED, NOT_ENROLLED, ERROR
            public string Message;
            public int Score;
        }

        public static Result Verify(string pcno, byte[] iso, byte[] ansi, int quality)
        {
            MfaConfig c = MfaConfig.Current;
            var repo = new MfaRepository(c);
            var r = new Result();
            List<byte[]> templates;
            try { templates = repo.GetTemplates(pcno); }
            catch (Exception ex)
            {
                r.Status = "ERROR";
                r.Message = "Database error. Please contact the administrator.";
                repo.Audit(pcno, "VERIFY", "ERROR", 0, quality, MfaLogin.ClientIp(), "DB: " + ex.Message);
                return r;
            }
            if (templates.Count == 0)
            {
                r.Status = "NOT_ENROLLED";
                r.Message = "No fingerprint is enrolled for your PC No. Please contact the administrator.";
                repo.Audit(pcno, "VERIFY", "NOT_ENROLLED", 0, quality, MfaLogin.ClientIp(), null);
                return r;
            }

            int best = -1;
            string lastErr = null;
            foreach (byte[] t in templates)
            {
                string err;
                if (c.TryIso && iso != null && iso.Length > 0)
                {
                    int s = ServerMatcher.Match(iso, t, false, out err);
                    if (s < 0) lastErr = err; else best = Math.Max(best, s);
                }
                if (best < c.MatchThreshold && c.TryAnsi && ansi != null && ansi.Length > 0)
                {
                    int s = ServerMatcher.Match(ansi, t, true, out err);
                    if (s < 0) lastErr = err; else best = Math.Max(best, s);
                }
                if (best >= c.MatchThreshold) break;
            }

            r.Score = Math.Max(best, 0);
            if (best < 0)
            {
                r.Status = "ERROR";
                r.Message = "Fingerprint matching is not available on the server. Please contact the administrator.";
                repo.Audit(pcno, "VERIFY", "ERROR", 0, quality, MfaLogin.ClientIp(), "Matcher: " + lastErr);
            }
            else if (best >= c.MatchThreshold)
            {
                r.Verified = true;
                r.Status = "VERIFIED";
                r.Message = "Fingerprint verified.";
                repo.Audit(pcno, "VERIFY", "VERIFIED", best, quality, MfaLogin.ClientIp(), null);
            }
            else
            {
                r.Status = "NOT_MATCHED";
                r.Message = "Fingerprint did not match.";
                repo.Audit(pcno, "VERIFY", "NOT_MATCHED", best, quality, MfaLogin.ClientIp(), null);
            }
            return r;
        }
    }
}
