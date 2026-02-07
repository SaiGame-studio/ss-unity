using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Represents gamer progress data from the API.
    /// game_data is a JSON string that can store custom game-specific data.
    /// Example: {"achievements":["first_login","level_10"],"current_quest":"defeat_boss"}
    /// </summary>
    [Serializable]
    public class GamerProgress
    {
        public string id;
        public string user_id;
        public string game_id;
        public int level;
        public int experience;
        public int gold;
        public string game_data;
        public long created_at;
        public long updated_at;
        public int version;
    }
}