using System;

namespace SaiGame.Services
{
    [Serializable]
    public class ItemTagData
    {
        public string id;
        public string studio_id;
        public string game_id;
        public string tag_key;
        public string label;
        public string color;
        public string metadata; // raw JSON object
        public string created_by;
        public string created_at;
        public string updated_at;
        public int item_count;
    }
}
