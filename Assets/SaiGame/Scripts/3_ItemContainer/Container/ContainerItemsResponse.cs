using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Represents the response returned by GET /api/v1/containers/{container_id}/items.
    /// </summary>
    [Serializable]
    public class ContainerItemsResponse
    {
        public string container_id;
        public InventoryItemData[] items;
    }
}
