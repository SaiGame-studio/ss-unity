using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Represents a single shop returned from the shops API.
    /// </summary>
    [Serializable]
    public class ShopData
    {
        public string id;
        public string studio_id;
        public string game_id;
        public string shop_key;
        public string name;
        public string description;
        public string shop_type;
        public bool is_active;
        public string currency_item_def_id;
        // Shop-level payment source: ShopCurrencyTypes.Item or ShopCurrencyTypes.SGem
        public string currency_type;
        public int item_count;
        public string starts_at;
        public string ends_at;
        public string created_at;
        public string updated_at;
    }
}
