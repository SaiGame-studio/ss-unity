using System;

namespace SaiGame.Services
{
    /// <summary>
    /// The purchase record returned inside a successful purchase response.
    /// </summary>
    [Serializable]
    public class PurchaseRecord
    {
        public string id;
        public string shop_id;
        public string shop_item_id;
        public string user_id;
        public string game_id;
        public int quantity;
        public int unit_price;
        public int total_price;
        public string idempotency_key;
        public string currency_item_def_id;
        // Payment source used for this purchase: ShopCurrencyTypes.Item or ShopCurrencyTypes.SGem
        public string currency_type;
        // sGem transaction id when currency_type is sgem
        public string sgem_tx_id;
        public string created_at;
    }
}
