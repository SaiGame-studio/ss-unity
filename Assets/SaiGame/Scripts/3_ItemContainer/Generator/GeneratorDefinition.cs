using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Represents the definition data for a generator item.
    /// </summary>
    [Serializable]
    public class GeneratorDefinition
    {
        public string item_code;
        public string name;
        public string rarity;
        public int grid_width;
        public int grid_height;
        public BaseStats base_stats;
        public GeneratorOutputPool[] output_pool;
    }

    [Serializable]
    public class BaseStats
    {
        // Add stats fields as needed based on your game design
    }
}
