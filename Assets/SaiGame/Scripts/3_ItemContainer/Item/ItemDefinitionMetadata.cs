using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Known fields from the item definition metadata object.
    /// The server returns metadata as a JSON object embedded in the definition.
    /// Unknown fields are silently ignored by JsonUtility.
    /// </summary>
    [Serializable]
    public class ItemDefinitionMetadata
    {
        public string flavor_text;
        public string icon;
        // Legacy single-pack field – kept for backward compatibility
        public string gacha_pack_id;
        // Multi-pack field: one gacha item can reference several gacha pack definitions
        public string[] gacha_pack_ids;
        // Recipe item: list of craft_recipe_input definition IDs this recipe unlocks
        public string[] craft_recipe_input_ids;
    }
}
