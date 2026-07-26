using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Response from GET /api/v1/games/{gameId}/daily-quests/pools/{poolKey}/assigned-timeframe.
    /// </summary>
    [Serializable]
    public class DailyTimeframeResponse
    {
        public string pool_id;
        public int days_ahead;
        public string start_date;
        public string end_date;
        public DailyDayData[] days;
    }
}
