using System;

namespace SaiGame.Services
{
    /// <summary>
    /// The full quest definition embedded inside a chain member.
    /// </summary>
    [Serializable]
    public class QuestDefinitionData
    {
        public string id;
        public string studio_id;
        public string game_id;
        public string code_name;
        public string name;
        public string description;
        public string quest_type;
        public QuestConditions conditions;
        public QuestReward[] rewards;
        public bool is_active;
        public bool is_hidden;
        public int sort_order;
        public string created_at;
        public string updated_at;
    }
}
