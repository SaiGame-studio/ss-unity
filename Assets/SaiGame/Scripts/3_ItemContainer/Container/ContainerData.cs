using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Represents a single container instance owned by the player.
    /// position_data is a raw JSON string to support arbitrary layout schemas.
    /// </summary>
    [Serializable]
    public class ContainerData
    {
        public string id;
        public string studio_id;
        public string game_id;
        public string owner_user_id;
        public string item_container_definition_id;
        public string container_type;
        // Raw JSON string – parse with your own deserializer as needed
        public string position_data;
        public string created_at;
        public string updated_at;
        public ContainerDefinitionData definition;
    }
}
