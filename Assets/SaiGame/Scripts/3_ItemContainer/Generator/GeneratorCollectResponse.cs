using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Response returned when collecting units from a generator.
    /// </summary>
    [Serializable]
    public class GeneratorCollectResponse
    {
        public int units_collected;
        public string output_item_code;
        public string output_inventory_item_id;
    }
}
