using System;

namespace SaiGame.Services
{
    [Serializable]
    public class GameGiftCodeInfo
    {
        public string id;
        public string game_id;
        public string code;
        public string[] gacha_pack_ids;
        public int max_uses;
        public int used_count;
        public string expires_at;
        public string active_at;
        public bool is_active;
        public string description;
    }

    [Serializable]
    public class GiftCodeResponse
    {
        public int total_items_granted;
        public GachaResponse[] results;
    }
}

