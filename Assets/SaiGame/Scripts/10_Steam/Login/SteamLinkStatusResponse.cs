using System;

namespace SaiGame.Services
{
    // POST /api/v1/player/auth/steam/status
    [Serializable]
    public class SteamLinkStatusResponse
    {
        public bool linked;
        public string steam_id;
    }
}
