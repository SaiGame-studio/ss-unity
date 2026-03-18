using System;

namespace SaiGame.Services
{
    [Serializable]
    public class LeaderboardBoard
    {
        public string id;
        public string studio_id;
        public string game_id;
        public string board_key;
        public string name;
        public string description;
        public string score_mode;
        public string sort_direction;
        public string reset_schedule;
        public string season_id;
        public bool is_active;
        public float max_score_delta;
        public string score_source_type;
        public string score_source_ref_id;
        public string created_at;
        public string updated_at;
    }

    [Serializable]
    public class LeaderboardBoardsResponse
    {
        public LeaderboardBoard[] boards;
    }

    [Serializable]
    public class LeaderboardBoardResponse
    {
        public LeaderboardBoard board;
    }

    [Serializable]
    public class LeaderboardRankingEntry
    {
        public int rank;
        public string user_id;
        public float score;
        public string metadata;
        public string updated_at;
    }

    [Serializable]
    public class LeaderboardRankingsResponse
    {
        public LeaderboardRankingEntry[] entries;
        public int limit;
        public int total;
    }

    [Serializable]
    public class LeaderboardSeason
    {
        public string id;
        public int season_number;
    }

    [Serializable]
    public class LeaderboardLocalRankingResponse
    {
        public int rank;
        public string user_id;
        public float score;
        public string metadata;
        public LeaderboardSeason season;
        public string updated_at;
    }
}
