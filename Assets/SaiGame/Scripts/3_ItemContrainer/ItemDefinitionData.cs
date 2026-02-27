using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Represents the item definition (template/blueprint) for an inventory item.
    /// base_stats and metadata are raw JSON strings to support arbitrary game-defined schemas.
    /// </summary>
    [Serializable]
    public class ItemDefinitionData
    {
        public string id;
        public string studio_id;
        public string game_id;
        public string item_code;
        public string name;
        public string category;
        public string rarity;
        // Raw JSON string – parse with your own deserializer as needed
        public string base_stats;
        // Raw JSON string – parse with your own deserializer as needed
        public string metadata;
        public bool is_stackable;
        public int max_stack_size;
        public int grid_width;
        public int grid_height;
        public string created_by;
        public string created_at;
        public string updated_at;
    }
}
