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
        public int item_count;
        public string starts_at;
        public string ends_at;
        public string created_at;
        public string updated_at;
    }

    /// <summary>
    /// Represents the paginated response from the shops API.
    /// </summary>
    [Serializable]
    public class ShopResponse
    {
        public ShopData[] shops;
        public int limit;
        public int offset;
        public int total;
    }

    /// <summary>
    /// Represents a single item listed in a shop.
    /// </summary>
    [Serializable]
    public class ShopItemData
    {
        public string id;
        public string shop_id;
        public string item_def_id;
        public string display_name;
        public string description;
        public int price;
        public string currency_item_def_id;
        public string purchase_limit_type;
        public int purchase_limit;
        public string restock_schedule;
        public int stock;
        public int sort_order;
        public bool is_active;
        public string available_from;
        public string available_until;
        public string created_at;
        public string updated_at;
    }

    /// <summary>
    /// Represents the response from the shop items API.
    /// </summary>
    [Serializable]
    public class ShopItemsResponse
    {
        public ShopItemData[] items;
        public int item_count;
        public string shop_id;
    }
}
