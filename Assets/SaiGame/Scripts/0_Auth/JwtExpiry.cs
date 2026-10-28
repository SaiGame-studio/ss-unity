using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SaiGame.Services
{
    /// <summary>
    /// Reads the "exp" claim of a JWT on the client so expired tokens are never sent to the server.
    /// The signature is not verified here; the server remains the authority.
    /// </summary>
    internal static class JwtExpiry
    {
        // Tokens expiring within this window count as expired, to absorb clock skew and request latency.
        public const double ExpirySkewSeconds = 10d;

        private static readonly Regex ExpClaim = new Regex("\"exp\"\\s*:\\s*(\\d+)");

        /// <summary>Returns the token expiry in UTC, or false when the token is not a readable JWT.</summary>
        public static bool TryGetExpiresAtUtc(string jwt, out DateTime expiresAtUtc)
        {
            expiresAtUtc = DateTime.MinValue;
            if (string.IsNullOrEmpty(jwt)) return false;

            string[] parts = jwt.Split('.');
            if (parts.Length < 2) return false;

            string payload;
            try
            {
                payload = Encoding.UTF8.GetString(DecodeBase64Url(parts[1]));
            }
            catch (FormatException)
            {
                return false;
            }

            Match match = ExpClaim.Match(payload);
            if (!match.Success) return false;
            if (!long.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long exp)) return false;

            try
            {
                expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// True when the token is empty, unreadable, or expires within ExpirySkewSeconds.
        /// </summary>
        public static bool IsExpired(string jwt) => IsExpiringWithin(jwt, ExpirySkewSeconds);

        /// <summary>
        /// True when the token is empty, unreadable, or has minRemainingSeconds or less left.
        /// </summary>
        public static bool IsExpiringWithin(string jwt, double minRemainingSeconds)
        {
            if (!TryGetExpiresAtUtc(jwt, out DateTime expiresAtUtc)) return true;
            return DateTime.UtcNow.AddSeconds(Math.Max(minRemainingSeconds, ExpirySkewSeconds)) >= expiresAtUtc;
        }

        private static byte[] DecodeBase64Url(string value)
        {
            string base64 = value.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            return Convert.FromBase64String(base64);
        }
    }
}
