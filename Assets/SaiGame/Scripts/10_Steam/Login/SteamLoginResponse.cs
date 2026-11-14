using System;
using SaiGame.Services;

namespace SaiGame.Auth.Steam
{
    [Serializable]
    public class SteamLoginResponse : LoginResponse
    {
        public SteamOwnership ownership;
        public SteamRewardResult reward;
    }
}
