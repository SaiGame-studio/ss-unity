using System;
using System.Globalization;
using UnityEngine;

namespace SaiGame.Services
{
    internal static class SaiAuthTokenStorage
    {
        private const string SessionKey = "SaiAuth.Session";
        private const string ExpiresAtKey = "SaiAuth.ExpiresAtUtcTicks";

        public static void Save(LoginResponse session, long expiresAt)
        {
            PlayerPrefs.SetString(SessionKey, JsonUtility.ToJson(session));
            PlayerPrefs.SetString(ExpiresAtKey, expiresAt.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
        }

        public static bool TryLoad(out LoginResponse session, out long expiresAt)
        {
            session = null;
            expiresAt = 0;
            string json = PlayerPrefs.GetString(SessionKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return false;

            try
            {
                session = JsonUtility.FromJson<LoginResponse>(json);
            }
            catch (ArgumentException)
            {
                Clear();
                return false;
            }

            if (session == null || string.IsNullOrEmpty(session.refresh_token))
            {
                Clear();
                return false;
            }

            // Missing or invalid expiry requires refreshing instead of trusting the access token.
            if (!long.TryParse(PlayerPrefs.GetString(ExpiresAtKey, string.Empty),
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out expiresAt)
                || expiresAt < DateTime.MinValue.Ticks || expiresAt > DateTime.MaxValue.Ticks)
                expiresAt = 0;

            return true;
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(SessionKey);
            PlayerPrefs.DeleteKey(ExpiresAtKey);
            PlayerPrefs.Save();
        }
    }
}
