using System;

namespace SaiGame.Services
{
    [Serializable]
    public class GoogleSessionPollResponse
    {
        public string status;
        public long expires_at;
        public string error;
        public UserData user;
        public string access_token;
        public string refresh_token;
        public int expires_in;
    }
}
