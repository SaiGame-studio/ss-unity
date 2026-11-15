using System;

namespace SaiGame.Services
{
    [Serializable]
    public class SteamLinkResponse : LoginResponse
    {
        public SteamOwnership ownership;
        public SteamRewardResult reward;
    }
}
