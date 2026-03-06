using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Identifies where the quest list is sourced from.
    /// Extend this enum when new quest types (e.g. Daily) are added.
    /// </summary>
    public enum QuestSourceType
    {
        ChainQuest,
        DailyQuest,
    }

    /// <summary>
    /// A lightweight descriptor used to populate the quest picker in the editor
    /// and at runtime, regardless of source type.
    /// </summary>
    [Serializable]
    public class QuestPickerEntry
    {
        public string questDefinitionId;
        public string displayName;
        public string sourceLabel; // e.g. "Chain 358"
    }

    /// <summary>
    /// Server response for POST /api/v1/games/{gameId}/quests/{questDefinitionId}/start
    /// </summary>
    [Serializable]
    public class QuestProgressRecord
    {
        public string id;
        public string studio_id;
        public string game_id;
        public string user_id;
        public string quest_definition_id;
        public string status;
        public int version;
        public string created_at;
        public string updated_at;
        // progress_data is a dynamic object; stored as raw JSON string if needed
    }

    /// <summary>
    /// Wrapper used to parse the start-quest response body.
    /// Maps directly to the flat response (no wrapper object).
    /// </summary>
    [Serializable]
    public class StartQuestResponse
    {
        public string id;
        public string studio_id;
        public string game_id;
        public string user_id;
        public string quest_definition_id;
        public string status;
        public int version;
        public string created_at;
        public string updated_at;
    }

    /// <summary>
    /// The progress snapshot returned by POST .../quests/{questDefinitionId}/check
    /// progress_data is a dynamic object and cannot be deserialized by JsonUtility.
    /// </summary>
    [Serializable]
    public class CheckQuestProgressRecord
    {
        public string id;
        public string studio_id;
        public string game_id;
        public string user_id;
        public string quest_definition_id;
        // progress_data is a dynamic key-value object; JsonUtility cannot deserialize it.
        // It is extracted manually as a raw JSON string after parsing.
        public string progress_data_json;
        public string status;
        public int version;
        public string created_at;
        public string updated_at;
    }

    /// <summary>
    /// Server response for POST /api/v1/games/{gameId}/quests/{questDefinitionId}/check
    /// </summary>
    [Serializable]
    public class CheckQuestResponse
    {
        public CheckQuestProgressRecord progress;
        public QuestDefinitionData quest_definition;
    }

    /// <summary>
    /// A single reward granted when claiming a completed quest.
    /// </summary>
    [Serializable]
    public class ClaimQuestGrantedReward
    {
        public string reward_type;
        public int amount;
        public int quantity;
        public string item_definition_id;
    }

    /// <summary>
    /// Server response for POST /api/v1/games/{gameId}/quests/{questDefinitionId}/claim
    /// </summary>
    [Serializable]
    public class ClaimQuestResponse
    {
        public string id;
        public string studio_id;
        public string game_id;
        public string user_id;
        public string quest_definition_id;
        public string progress_id;
        public string idempotency_key;
        public ClaimQuestGrantedReward[] rewards_granted;
        public string claimed_at;
    }
}
