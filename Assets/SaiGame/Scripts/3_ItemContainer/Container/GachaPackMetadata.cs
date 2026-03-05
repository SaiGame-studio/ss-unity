using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Subset of an item definition's metadata JSON used to determine if the item is openable as a gacha pack.
    /// Supports both a single gacha_pack_id (legacy) and an array gacha_pack_ids.
    /// </summary>
    [Serializable]
    public class GachaPackMetadata
    {
        // Legacy single-pack field – kept for backward compatibility
        public string gacha_pack_id;
        // Multi-pack field: one gacha item can reference several gacha pack definitions
        public string[] gacha_pack_ids;
    }
}
