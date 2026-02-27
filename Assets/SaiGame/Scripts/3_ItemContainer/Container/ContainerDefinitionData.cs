using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Represents the definition (template/blueprint) for an item container.
    /// metadata is a raw JSON string to support arbitrary game-defined schemas.
    /// </summary>
    [Serializable]
    public class ContainerDefinitionData
    {
        public string id;
        public string studio_id;
        public string game_id;
        public string name;
        public string container_type;
        public int grid_cols;
        public int grid_rows;
        public bool is_portable;
        // Raw JSON string – parse with your own deserializer as needed
        public string metadata;
        public string created_at;
        public string updated_at;
    }
}
