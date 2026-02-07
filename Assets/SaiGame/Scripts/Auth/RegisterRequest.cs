using System;

namespace SaiGame.Services
{
    [Serializable]
    public class RegisterRequest
    {
        public string email;
        public string username;
        public string password;
    }
}
