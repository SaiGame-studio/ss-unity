using System;

namespace SaiGame.Services
{
    [Serializable]
    public class BattlePassResponse
    {
        public BattlePassData[] pools;
        public int limit;
        public int offset;
        public int total;
    }
}
