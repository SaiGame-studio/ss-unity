namespace SaiGame.Services
{
    /// <summary>
    /// Payment source values returned by the shop API in currency_type / payment_currency_type.
    /// </summary>
    public static class ShopCurrencyTypes
    {
        /// <summary>Inventory item currency (currency_item_def_id), or the legacy coin wallet when that id is empty.</summary>
        public const string Item = "item";

        /// <summary>The player's platform sGem wallet.</summary>
        public const string SGem = "sgem";

        /// <summary>Display label for the sGem currency.</summary>
        public const string SGemLabel = "sGem";
    }
}
