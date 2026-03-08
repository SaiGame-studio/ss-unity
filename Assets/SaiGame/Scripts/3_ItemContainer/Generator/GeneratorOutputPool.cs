using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Represents a single output entry in a generator's output pool.
    /// </summary>
    [Serializable]
    public class GeneratorOutputPool
    {
        public string item_definition_id;
        public float drop_rate;
        public int quantity_min;
        public int quantity_max;
        public int collect_cap;
        public int initial_output;
    }
}
