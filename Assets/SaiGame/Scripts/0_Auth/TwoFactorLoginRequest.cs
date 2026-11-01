using System;

namespace SaiGame.Services
{
    [Serializable]
    public class TwoFactorLoginRequest
    {
        public string user_id;
        public string challenge_token;
        public string code;
    }
}
