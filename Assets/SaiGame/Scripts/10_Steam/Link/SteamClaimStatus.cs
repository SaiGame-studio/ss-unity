using System;

namespace SaiGame.Services
{
    [Serializable]
    public class SteamClaimStatus
    {
        public bool claimed;
        public bool can_claim;
        public string message_code;
    }
}
