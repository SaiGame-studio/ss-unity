using System;

namespace SaiGame.Services
{
    [Serializable]
    public class SteamTicketRequest
    {
        public string game_id;
        public string ticket;
        // Link only: required when the account has 2FA enabled.
        public string code;
        public string recovery_code;
    }
}
