using System;

namespace SaiGame.Services
{
    [Serializable]
    public class SteamLinkResponse : LoginResponse
    {
        public SteamOwnership ownership;
        // Claim Game result; the JSON key is "reward" on the Sai Server.
        public SteamClaimGameResult reward;
    }
}
