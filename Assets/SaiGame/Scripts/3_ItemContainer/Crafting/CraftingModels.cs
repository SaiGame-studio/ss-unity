using System;

namespace SaiGame.Services
{
    [Serializable]
    public class CraftRequest
    {
        public string recipe_id;
        public string idempotency_key;
    }

    [Serializable]
    public class CraftByKeyRequest
    {
        public string recipe_key;
        public string idempotency_key;
    }

    [Serializable]
    public class CraftingOutputItem
    {
        public string item_definition_id;
        public string item_definition_name;
        public int quantity;
    }

    [Serializable]
    public class CraftingMaterialItem
    {
        public string item_definition_id;
        public string item_definition_name;
        public int quantity;
        public bool was_consumed;
    }

    [Serializable]
    public class CraftingResponse
    {
        public string transaction_id;
        public bool success;
        public bool bonus_triggered;
        public CraftingOutputItem[] output_items;
        public CraftingMaterialItem[] materials_used;
    }

    [Serializable]
    public class RecipeInput
    {
        public string id;
        public string recipe_id;
        public string studio_id;
        public string game_id;
        public string item_definition_id;
        public int quantity;
        public bool is_consumed;
        public string created_at;
        public string updated_at;
        public ItemDefinitionData item_definition;
    }

    [Serializable]
    public class RecipeOutput
    {
        public string id;
        public string recipe_id;
        public string studio_id;
        public string game_id;
        public string item_definition_id;
        public int quantity_min;
        public int quantity_max;
        public string output_type;
        public int sort_order;
        public string created_at;
        public string updated_at;
        public ItemDefinitionData item_definition;
    }

    [Serializable]
    public class RecipeMetadata
    {
        public string difficulty;
        public string icon;
    }

    [Serializable]
    public class RecipeDetail
    {
        public string id;
        public string studio_id;
        public string game_id;
        public string recipe_key;
        public string name;
        public string description;
        public string category;
        public int success_rate;
        public int bonus_rate;
        public bool is_active;
        public RecipeMetadata metadata;
        public string created_by;
        public string created_at;
        public string updated_at;
        public RecipeInput[] inputs;
        public RecipeOutput[] outputs;
    }

    [Serializable]
    public class CraftingHistoryTransaction
    {
        public string id;
        public string studio_id;
        public string game_id;
        public string user_id;
        public string recipe_id;
        public string idempotency_key;
        public string status;
        public bool success;
        public bool bonus_triggered;
        public CraftingMaterialItem[] materials_snapshot;
        public CraftingOutputItem[] outputs_snapshot;
        public string created_at;
        public RecipeDetail recipe_detail;
    }

    [Serializable]
    public class CraftingHistoryResponse
    {
        public int page;
        public int page_size;
        public int total;
        public CraftingHistoryTransaction[] transactions;
    }
}
