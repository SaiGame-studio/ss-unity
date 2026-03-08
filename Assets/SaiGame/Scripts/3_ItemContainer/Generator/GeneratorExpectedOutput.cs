using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Represents the expected output for a single item in a generator's output pool.
    /// </summary>
    [Serializable]
    public class GeneratorExpectedOutput
    {
        public string item_definition_id;
        public float drop_rate;
        public int quantity_min;
        public int quantity_max;
        public int collect_cap;
        public int expected_min;
        public int expected_max;
        public int current_pending_ticks;
    }
}
