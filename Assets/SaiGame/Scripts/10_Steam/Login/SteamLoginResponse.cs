using System;

namespace SaiGame.Services
{
    [Serializable]
    public class SteamLoginResponse : LoginResponse
    {
        public SteamOwnership ownership;
        public SteamRewardResult reward;
    }
}
