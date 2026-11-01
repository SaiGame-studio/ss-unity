using System;

namespace SaiGame.Services
{
    [Serializable]
    public class LoginResponse
    {
        public UserData user;
        public string access_token;
        public string refresh_token;
        public int expires_in;
        public string message_code;
        public string challenge_token;
    }
}
