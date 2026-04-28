using System;
using UnityEngine;

namespace SaiGame.Services
{
    /// <summary>
    /// Represents the item definition (template/blueprint) for an inventory item.
    /// base_stats and metadata are raw JSON strings pre-processed by InventoryJsonHelper.
    /// Use ParsedMetadata for typed access to known metadata fields.
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
        // Raw JSON string pre-processed by InventoryJsonHelper
        public string metadata;

        // Typed access to known metadata fields (gacha_pack_ids, craft_recipe_input_ids, etc.)
        public ItemDefinitionMetadata ParsedMetadata =>
            string.IsNullOrEmpty(this.metadata) ? null : JsonUtility.FromJson<ItemDefinitionMetadata>(this.metadata);
        public bool is_stackable;
        public int max_stack_size;
        public int grid_width;
        public int grid_height;
        public bool client_writable;
        public bool allow_client_update_qty;
        public string created_by;
        public string created_at;
        public string updated_at;
    }
}
