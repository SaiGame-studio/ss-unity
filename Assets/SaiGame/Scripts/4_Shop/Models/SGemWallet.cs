using System;

namespace SaiGame.Services
{
    /// <summary>
    /// The player's sGem wallet.
    /// Endpoint: GET /api/v1/me/sgem-wallet
    /// </summary>
    [Serializable]
    public class SGemWallet
    {
        public string id;
        public string user_id;
        public long balance;
        public long total_bought;
        public long total_spent;
        public string created_at;
        public string updated_at;
    }
}
