using System;

namespace SaiGame.Services
{
    [Serializable]
    public class SteamOwnership
    {
        public string steam_id;
        public uint app_id;
        public bool owns_app;
        public bool permanent;
        public string owner_steam_id;
    }
}
