using System;

namespace SaiGame.Services
{
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
        // Per-item payment source override; empty = inherit the shop
        public string currency_type;
        // Resolved payment source for this item: ShopCurrencyTypes.Item or ShopCurrencyTypes.SGem
        public string payment_currency_type;
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
        // Only present in response when a limit type is set (player/global)
        public int purchased_count;

        public bool IsPaidWithSGem => this.payment_currency_type == ShopCurrencyTypes.SGem;
    }
}
