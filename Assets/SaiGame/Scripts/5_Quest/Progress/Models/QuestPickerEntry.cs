using System;

namespace SaiGame.Services
{
    /// <summary>
    /// A lightweight descriptor used to populate the quest picker in the editor
    /// and at runtime, regardless of source type.
    /// </summary>
    [Serializable]
    public class QuestPickerEntry
    {
        public string questDefinitionId;
        public string dailyQuestAssignmentId;
        public string displayName;
        public string sourceLabel; // e.g. "Chain 358"
    }
}
