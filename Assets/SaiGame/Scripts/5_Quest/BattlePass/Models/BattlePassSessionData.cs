using System;

namespace SaiGame.Services
{
    [Serializable]
    public class BattlePassSessionData
    {
        public string schedule_mode;
        public string session_start_at;
        public string session_end_at;
        public string cycle_start_at;
        public string repeat_type;
        public int repeat_amount;
    }
}
