using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Represents a single item granted from opening a gacha pack.
    /// </summary>
    [Serializable]
    public class GachaItemGranted
    {
        public string item_definition_id;
        public string name;
        public string category;
        public int quantity;
        public int quantity_min;
        public int quantity_max;
        // UUID of the newly-created inventory item (if immediately placed in inventory)
        public string inventory_item_id;
        public string drop_seed;
        public string qty_seed;
    }
}
