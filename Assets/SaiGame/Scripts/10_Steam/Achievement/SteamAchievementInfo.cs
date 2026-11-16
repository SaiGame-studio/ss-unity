using System;

namespace SaiGame.Services
{
    // One achievement as defined in Steamworks, with the local player's state.
    [Serializable]
    public class SteamAchievementInfo
    {
        public string api_name;
        public string display_name;
        public string description;
        public bool hidden;
        public bool achieved;
        // Unix time in seconds; 0 when not achieved.
        public uint unlock_time;
    }
}
