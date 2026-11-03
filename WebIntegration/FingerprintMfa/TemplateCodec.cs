using System;
using System.Text;
using System.Text.RegularExpressions;

namespace FingerprintMfa
{
    /// <summary>Turns whatever is stored in Oracle (BLOB bytes, Base64 or HEX text) into template bytes.</summary>
    internal static class TemplateCodec
    {
        public static byte[] Decode(object value, string encoding)
        {
            string enc = (encoding ?? "AUTO").ToUpperInvariant();
            byte[] bytes = value as byte[];
            if (bytes != null)
            {
                if (enc == "BASE64" || enc == "HEX") return DecodeText(Encoding.ASCII.GetString(bytes), enc);
                if (enc == "AUTO" && !IsFmr(bytes) && IsAscii(bytes))
                {
                    try { return DecodeText(Encoding.ASCII.GetString(bytes), "AUTO"); } catch { }
                }
                return bytes;
            }
            return DecodeText(Convert.ToString(value), enc == "RAW" ? "AUTO" : enc);
        }

        public static bool IsFmr(byte[] b)
        {
            return b != null && b.Length >= 4 && b[0] == 0x46 && b[1] == 0x4D && b[2] == 0x52 && b[3] == 0x00;
        }

        private static bool IsAscii(byte[] b)
        {
            int n = Math.Min(b.Length, 256);
            for (int i = 0; i < n; i++)
            {
                byte c = b[i];
                if (c == 9 || c == 10 || c == 13) continue;
                if (c < 0x20 || c > 0x7E) return false;
            }
            return b.Length > 0;
        }

        private static byte[] DecodeText(string text, string enc)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            text = text.Trim();
            bool hex = text.Length % 2 == 0 && Regex.IsMatch(text, "^[0-9A-Fa-f]+$");
            if (enc == "HEX" || (enc == "AUTO" && hex))
            {
                var b = new byte[text.Length / 2];
                for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(text.Substring(i * 2, 2), 16);
                return b;
            }
            return Convert.FromBase64String(text);
        }
    }
}
