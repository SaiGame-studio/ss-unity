using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Server response for POST /api/v1/games/{gameId}/quests/{questDefinitionId}/check
    /// </summary>
    [Serializable]
    public class CheckQuestResponse
    {
        public CheckQuestProgressRecord progress;
        public QuestDefinitionData quest_definition;
        /// <summary>Quest status from the response (e.g. not_started, in_progress, completed, claimed).</summary>
        public string status;
    }
}
