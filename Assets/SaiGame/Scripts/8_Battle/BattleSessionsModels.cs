using System;
using UnityEngine;

namespace SaiGame.Services
{
    [Serializable]
    public class BattleSessionsResponse
    {
        public int limit;
        public int offset;
        public int total;
        public BattleSessionData[] sessions;
    }

    [Serializable]
    public class BattleSessionData
    {
        public string id;
        public string game_id;
        public string player_id;
        public string status;
        public string started_at;
        public string expires_at;
        public string ended_at;
        public BattleStartData start_data;
        public BattleEndData end_data;
    }

    [Serializable]
    public class BattleStartData
    {
        public string[] battle_log;
        public bool battle_over;
        public BattleEnemy[] enemies;
        public BattlePlayerChar[] player_chars;
        public int turn;
        public bool victory;
    }

    [Serializable]
    public class BattleEndData
    {
        public int kills;
        public string summary;
        public int survival_pct;
        public int turns_taken;
        public bool victory;
    }

    [Serializable]
    public class BattleEnemy
    {
        public bool alive;
        public int attack;
        public int defense;
        public int hp;
        public string id;
        public int max_hp;
        public string name;
        public int position;
        public int speed;
    }

    [Serializable]
    public class BattlePlayerChar
    {
        public bool alive;
        public int attack;
        public int defense;
        public int hp;
        public string id;
        public int max_hp;
        public int mp;
        public string name;
    }
}
