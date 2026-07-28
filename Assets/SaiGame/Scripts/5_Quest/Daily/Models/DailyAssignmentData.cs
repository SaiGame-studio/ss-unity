using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Represents the assignment record for a daily quest.
    /// </summary>
    [Serializable]
    public class DailyAssignmentData
    {
        public string id;
        public string studio_id;
        public string game_id;
        public string user_id;
        public string pool_id;
        public string quest_definition_id;
        public string assigned_date;
        public string available_at;
        public string expires_at;
        public string created_at;
    }
}
