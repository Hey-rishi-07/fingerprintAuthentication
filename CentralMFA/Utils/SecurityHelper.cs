using System;
using System.Security.Cryptography;
using System.Text;

namespace CentralMFA.Utils
{
    public static class SecurityHelper
    {
        /// <summary>
        /// Generates an HMAC-SHA256 signature in hexadecimal format for a given message string and secret key.
        /// </summary>
        public static string ComputeHmacSha256(string message, string secretKey)
        {
            if (string.IsNullOrEmpty(message) || string.IsNullOrEmpty(secretKey))
                return string.Empty;

            byte[] keyBytes = Encoding.UTF8.GetBytes(secretKey);
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);

            using (var hmac = new HMACSHA256(keyBytes))
            {
                byte[] hash = hmac.ComputeHash(messageBytes);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Validates an HMAC-SHA256 signature using constant-time comparison to protect against timing attacks.
        /// </summary>
        public static bool VerifyHmacSha256(string message, string signature, string secretKey)
        {
            if (string.IsNullOrEmpty(message) || string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(secretKey))
                return false;

            string expected = ComputeHmacSha256(message, secretKey);
            if (expected.Length != signature.Length) return false;

            int result = 0;
            for (int i = 0; i < expected.Length; i++)
            {
                result |= expected[i] ^ signature[i];
            }
            return result == 0;
        }

        /// <summary>
        /// Generates a secure cryptographic random nonce.
        /// </summary>
        public static string GenerateNonce(int length = 16)
        {
            byte[] bytes = new byte[length];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(bytes);
            }
            StringBuilder sb = new StringBuilder();
            foreach (byte b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Checks if a unix epoch timestamp is within the allowed window (default: 120 seconds).
        /// </summary>
        public static bool IsTimestampValid(long timestamp, int maxAgeSeconds = 120)
        {
            long currentEpoch = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            return Math.Abs(currentEpoch - timestamp) <= maxAgeSeconds;
        }

        /// <summary>
        /// Checks if a string timestamp (either unix epoch or yyyyMMddHHmmss) is within the allowed window.
        /// </summary>
        public static bool IsTimestampValid(string timestampStr, int maxAgeSeconds = 120)
        {
            if (string.IsNullOrEmpty(timestampStr)) return false;
            long epoch;
            if (long.TryParse(timestampStr, out epoch))
            {
                return IsTimestampValid(epoch, maxAgeSeconds);
            }
            DateTime dt;
            if (DateTime.TryParseExact(timestampStr, "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out dt))
            {
                return Math.Abs((DateTime.UtcNow - dt.ToUniversalTime()).TotalSeconds) <= maxAgeSeconds;
            }
            return false;
        }

        /// <summary>
        /// Gets the current Unix timestamp in seconds.
        /// </summary>
        public static long GetCurrentEpoch()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
        }

        /// <summary>
        /// Constructs a standardized message payload string for signing.
        /// </summary>
        public static string BuildPayload(string appId, string pcno, string status, string action, string txnId, long timestamp, string nonce)
        {
            return string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}",
                appId ?? "",
                pcno ?? "",
                status ?? "",
                action ?? "",
                txnId ?? "",
                timestamp,
                nonce ?? "");
        }

        /// <summary>
        /// Constructs a standardized message payload string for signing.
        /// </summary>
        public static string BuildPayload(string appId, string pcno, string status, string action, string txnId, string timestamp, string nonce)
        {
            return string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}",
                appId ?? "",
                pcno ?? "",
                status ?? "",
                action ?? "",
                txnId ?? "",
                timestamp ?? "",
                nonce ?? "");
        }
    }
}
