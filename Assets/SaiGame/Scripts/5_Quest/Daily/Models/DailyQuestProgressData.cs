using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Progress block embedded in a DailyQuestEntryData.
    /// Maps to the "progress" object inside each entry of the today-quest response.
    /// progress_data is dynamic JSON; stored as raw string in progress_data_json (parsed manually).
    /// </summary>
    [Serializable]
    public class DailyQuestProgressData
    {
        public string id;
        public string studio_id;
        public string game_id;
        public string user_id;
        public string quest_definition_id;
        public string status;
        public string completed_at;
        public string claimed_at;
        public string reset_at;
        public int version;
        public string created_at;
        public string updated_at;

        [NonSerialized] public string progress_data_json;
    }
}
