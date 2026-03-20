using System;

namespace SaiGame.Services
{
    [Serializable]
    public class PresetDefinition
    {
        public string id;
        public string preset_type;
        public string name;
        public int max_slots;
        public string created_at;
        public string updated_at;
    }

    [Serializable]
    public class PresetData
    {
        public string id;
        public string definition_id;
        public PresetDefinition definition;
        public string preset_type;
        public int max_slots;
        public bool is_temp;
        public PresetSlotData[] slots;
        public string created_at;
        public string updated_at;
    }

    [Serializable]
    public class PresetSlotData
    {
        public int slot_index;
        public string inventory_item_id;
    }

    [Serializable]
    public class PresetDetailResponse
    {
        public PresetData container;
        public PresetSlotData[] slots;
    }
}
