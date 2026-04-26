using System;

namespace SaiGame.Services
{
    /// <summary>
    /// A single entry pairing an assignment with its quest definition.
    /// </summary>
    [Serializable]
    public class DailyQuestEntryData
    {
        public DailyAssignmentData assignment;
        public QuestDefinitionData quest;
        /// <summary>Quest progress status: not_started | in_progress | completed | claimed</summary>
        public string status;
        public DailyQuestProgressData progress;
        /// <summary>Resolved rewards with item definitions populated by the server.</summary>
        public DailyRewardData[] rewards;
    }
}
