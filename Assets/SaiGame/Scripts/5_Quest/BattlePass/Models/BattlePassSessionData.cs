using System;

namespace SaiGame.Services
{
    [Serializable]
    public class BattlePassSessionData
    {
        public string cycle_start_at;
        public int repeat_every_months;
        public bool repeatable;
    }
}
