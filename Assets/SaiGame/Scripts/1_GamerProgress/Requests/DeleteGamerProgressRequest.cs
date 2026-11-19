using System;

namespace SaiGame.Services
{
    // Body for DELETE /api/v1/games/{game_id}/my-gamer-progress when the account has 2FA enabled.
    [Serializable]
    public class DeleteGamerProgressRequest
    {
        public string code;
    }
}
