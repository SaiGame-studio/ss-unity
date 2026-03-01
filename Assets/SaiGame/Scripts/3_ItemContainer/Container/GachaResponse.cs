using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Represents the response from the gacha pack opening endpoint.
    /// Endpoint: POST /api/v1/games/{game_id}/gacha/{gacha_pack_id}
    /// </summary>
    [Serializable]
    public class GachaResponse
    {
        // True when the same idempotency_key was already processed – no new items were granted
        public bool is_duplicate;
        public GachaItemGranted[] items_granted;
        public string mailbox_message_id;
        public string transaction_id;
    }
}
