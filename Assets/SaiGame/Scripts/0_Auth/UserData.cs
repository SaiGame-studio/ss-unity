using System;

namespace SaiGame.Services
{
    [Serializable]
    public class UserData
    {
        public string id;
        public string email;
        public string username;
        public string display_name;
        public bool is_active;
        public bool is_verified;
        public long created_at;
    }
}
