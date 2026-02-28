using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Represents the paginated response returned by the inventory endpoint.
    /// </summary>
    [Serializable]
    public class InventoryResponse
    {
        public InventoryItemData[] items;
        public int limit;
        public int offset;
        public int total;
    }

    /// <summary>
    /// Response from GET /api/v1/items/categories
    /// </summary>
    [Serializable]
    public class ItemCategoriesResponse
    {
        public string[] categories;
    }
}
