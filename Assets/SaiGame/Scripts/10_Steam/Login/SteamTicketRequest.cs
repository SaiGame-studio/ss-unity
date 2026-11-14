using System;

namespace SaiGame.Auth.Steam
{
    [Serializable]
    public class SteamTicketRequest
    {
        public string game_id;
        public string ticket;
    }
}
