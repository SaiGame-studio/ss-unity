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
        // Present only on gacha_pack items – used as the gacha_pack_id URL parameter
        public string gacha_pack_id;
    }
}
