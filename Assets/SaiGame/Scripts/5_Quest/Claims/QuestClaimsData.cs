using System;

namespace SaiGame.Services
{
    /// <summary>
    /// A single quest claim record returned by the quest-claims API.
    /// Maps to each entry in GET /api/v1/games/{gameId}/quest-claims
    /// </summary>
    [Serializable]
    public class QuestClaimRecord
    {
        public string id;
        public string studio_id;
        public string game_id;
        public string user_id;
        public string quest_definition_id;
        public string progress_id;
        public string idempotency_key;
        // Reuses ClaimQuestGrantedReward defined in QuestProgressorData.cs
        public ClaimQuestGrantedReward[] rewards_granted;
        public string claimed_at;
        // Reuses QuestDefinitionData defined in ChainQuestData.cs
        public QuestDefinitionData quest_definition;
    }

    /// <summary>
    /// Paginated response from GET /api/v1/games/{gameId}/quest-claims
    /// </summary>
    [Serializable]
    public class QuestClaimsResponse
    {
        public QuestClaimRecord[] claims;
        public int limit;
        public int offset;
        public int total;
    }
}
