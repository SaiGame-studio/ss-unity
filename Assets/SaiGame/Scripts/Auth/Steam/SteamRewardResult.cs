using System;

namespace SaiGame.Auth.Steam
{
    [Serializable]
    public class SteamRewardResult
    {
        public string status;
        public string reward_code;
        public string message_code;
    }
}
