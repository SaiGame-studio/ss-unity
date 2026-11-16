using System;

namespace SaiGame.Services
{
    // The Steam part of GET /api/v1/public/games/{game_id}/info.
    [Serializable]
    public class SteamPublicGameInfo
    {
        // 0 when Steam is not enabled for the game (the server sends null).
        public long steam_app_id;
    }
}
