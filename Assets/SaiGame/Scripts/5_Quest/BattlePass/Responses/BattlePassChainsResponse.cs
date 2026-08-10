using System;

namespace SaiGame.Services
{
    [Serializable]
    public class BattlePassChainsResponse
    {
        public BattlePassData pool;
        public string pool_state;
        public BattlePassChainData[] chains;
    }
}
