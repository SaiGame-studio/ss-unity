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
        public string expires_at;
        public string created_at;
    }

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
    }

    /// <summary>
    /// Represents a single day in the assign-ahead response,
    /// containing the date metadata and list of assigned quests.
    /// </summary>
    [Serializable]
    public class DailyDayData
    {
        public string date;
        public bool is_today;
        public bool already_assigned;
        public DailyQuestEntryData[] quests;
    }

    /// <summary>
    /// A single daily quest pool returned from the pools API.
    /// </summary>
    [Serializable]
    public class DailyQuestPoolData
    {
        public string id;
        public string studio_id;
        public string game_id;
        public string pool_key;
        public string display_name;
        public string description;
        public int slots_per_day;
        public int reset_hour_utc;
        public string assignment_strategy;
        public bool is_active;
        public string created_at;
        public string updated_at;
    }

    /// <summary>
    /// Paginated response from GET /api/v1/games/{gameId}/daily-quest-pools
    /// </summary>
    [Serializable]
    public class DailyQuestPoolsResponse
    {
        public DailyQuestPoolData[] pools;
        public int limit;
        public int offset;
        public int total;
    }

    /// <summary>
    /// Streak record returned inside the today-quest response.
    /// </summary>
    [Serializable]
    public class DailyStreakData
    {
        public string id;
        public string studio_id;
        public string game_id;
        public string user_id;
        public string pool_id;
        public int current_streak;
        public int longest_streak;
        public int total_completions;
        public int version;
        public string created_at;
        public string updated_at;
    }

    /// <summary>
    /// Response from GET /api/v1/games/{gameId}/daily-quests/{dqPoolId}
    /// </summary>
    [Serializable]
    public class TodayQuestResponse
    {
        public DailyQuestPoolData pool;
        public DailyQuestEntryData[] entries;
        public DailyStreakData streak;
        public string assigned_date;
    }

    /// <summary>
    /// Request body for POST /api/v1/games/{gameId}/daily-quests/{dqPoolId}/assign-ahead
    /// </summary>
    [Serializable]
    public class AssignAheadRequest
    {
        public int days_ahead;
    }

    /// <summary>
    /// Full response from POST /api/v1/games/{gameId}/daily-quests/{dqPoolId}/assign-ahead
    /// </summary>
    [Serializable]
    public class AssignAheadResponse
    {
        public string pool_id;
        public int days_ahead;
        public string start_date;
        public string end_date;
        public DailyDayData[] days;
    }
}
