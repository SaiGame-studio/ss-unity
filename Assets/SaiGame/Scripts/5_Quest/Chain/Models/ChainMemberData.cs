using System;

namespace SaiGame.Services
{
    /// <summary>
    /// A single member entry inside a quest chain.
    /// </summary>
    [Serializable]
    public class ChainMemberData
    {
        public string id;
        public string chain_id;
        public string quest_definition_id;
        public int sort_order;
        public string[] unlock_quest_ids;
        public QuestDefinitionData definition;
        public string status;
        public string created_at;
        public string updated_at;
    }
}
