using System;

namespace SaiGame.Services
{
    [Serializable]
    public class GoogleSessionRequest
    {
        public string game_id;
        public string platform;
        public string client_fingerprint;
    }
}
