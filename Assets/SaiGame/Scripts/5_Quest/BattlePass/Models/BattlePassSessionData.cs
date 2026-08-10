using System;

namespace SaiGame.Services
{
    [Serializable]
    public class BattlePassSessionData
    {
        public bool repeatable;
        public string session_start_at;
        public string session_end_at;
        public string cycle_start_at;
        public int repeat_every_months;
    }
}
