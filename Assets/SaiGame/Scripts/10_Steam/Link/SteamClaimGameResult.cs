using System;

namespace SaiGame.Services
{
    // Result of Claim Game: the game's Steam owner items. They belong to the
    // Steam account and move with it when it is unlinked and linked again.
    // status: granted, transferred, already_granted, pending, ineligible, not_configured.
    [Serializable]
    public class SteamClaimGameResult
    {
        public string status;
        public string message_code;
    }
}
