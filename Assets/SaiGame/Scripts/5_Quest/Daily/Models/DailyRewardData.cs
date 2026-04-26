using System;

namespace SaiGame.Services
{
    /// <summary>
    /// A single reward entry on a daily quest assignment, including the resolved item definition.
    /// </summary>
    [Serializable]
    public class DailyRewardData
    {
        public string reward_type;
        public string item_definition_id;
        public int quantity_min;
        public int quantity_max;
        public ItemDefinitionData item_definition;
    }
}
