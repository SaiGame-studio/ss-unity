using System;

namespace SaiGame.Services
{
    [Serializable]
    public class GoogleSessionResponse
    {
        public string session_id;
        public string auth_url;
        public long expires_at;
        public int poll_interval_seconds;
    }
}
