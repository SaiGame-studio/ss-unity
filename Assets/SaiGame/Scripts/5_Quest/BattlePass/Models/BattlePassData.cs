using System;

namespace SaiGame.Services
{
    [Serializable]
    public class BattlePassData
    {
        public string id;
        public string game_id;
        public string pool_key;
        public string display_name;
        public string description;
        public bool is_active;
        public BattlePassTypeConfig type_config;
        public string created_at;
        public string updated_at;
    }
}
