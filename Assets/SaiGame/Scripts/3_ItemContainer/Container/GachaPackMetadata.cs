using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Subset of an item definition's metadata JSON used to determine if the item is openable as a gacha pack.
    /// If gacha_pack_id is present and non-empty, the item can be opened via the gacha endpoint.
    /// </summary>
    [Serializable]
    public class GachaPackMetadata
    {
        public string gacha_pack_id;
    }
}
