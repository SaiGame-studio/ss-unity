using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Represents a single item instance inside a player's inventory container.
    /// custom_properties is a raw JSON string to support arbitrary game-defined extra data.
    /// </summary>
    [Serializable]
    public class InventoryItemData
    {
        public string id;
        public string studio_id;
        public string game_id;
        public string user_id;
        public string item_definition_id;
        public string item_container_id;
        public int grid_x;
        public int grid_y;
        public int quantity;
        public int level;
        // Raw JSON string – may be null/empty when the server returns null
        public string custom_properties;
        // Used by container items endpoint (private_properties / public_properties)
        public string private_properties;
        public string public_properties;
        public string acquired_at;
        public string last_modified_at;
        public int version;
        public ItemDefinitionData definition;
    }
}
