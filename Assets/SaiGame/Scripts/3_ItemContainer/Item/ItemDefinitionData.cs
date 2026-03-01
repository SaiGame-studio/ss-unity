using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Represents the item definition (template/blueprint) for an inventory item.
    /// base_stats is a raw JSON string. metadata is a typed object deserialized by JsonUtility.
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
        // Typed object – fields unknown to JsonUtility are silently ignored
        public ItemDefinitionMetadata metadata;
        public bool is_stackable;
        public int max_stack_size;
        public int grid_width;
        public int grid_height;
        public string created_by;
        public string created_at;
        public string updated_at;
    }
}
