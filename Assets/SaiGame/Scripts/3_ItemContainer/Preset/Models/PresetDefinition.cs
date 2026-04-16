using System;

namespace SaiGame.Services
{
    [Serializable]
    public class PresetDefinition
    {
        public string id;
        public string code_name;
        public string preset_type;
        public string name;
        public int max_slots;
        public string created_at;
        public string updated_at;
    }
}
