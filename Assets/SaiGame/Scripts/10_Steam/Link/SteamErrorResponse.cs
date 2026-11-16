using System;

namespace SaiGame.Services
{
    [Serializable]
    public class SteamErrorResponse
    {
        public string message_code;
        public string relink_available_at;
        public long retry_after_seconds;
    }
}
