using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Represents a single quest chain returned from the chains API.
    /// </summary>
    [Serializable]
    public class ChainQuestData
    {
        public string id;
        public string studio_id;
        public string game_id;
        public string chain_key;
        public string display_name;
        public string description;
        public string chain_type;
        public bool is_active;
        public string created_at;
        public string updated_at;
    }

    /// <summary>
    /// Represents the paginated response from the quest chains API.
    /// </summary>
    [Serializable]
    public class ChainQuestResponse
    {
        public ChainQuestData[] chains;
        public int limit;
        public int offset;
        public int total;
    }

    // ── Chain Members ──────────────────────────────────────────────────────────

    /// <summary>
    /// An item required inside a quest condition clause.
    /// </summary>
    [Serializable]
    public class QuestClauseItem
    {
        public string item_definition_id;
        public int quantity;
    }

    /// <summary>
    /// A gacha pack requirement inside a quest condition clause.
    /// </summary>
    [Serializable]
    public class QuestClausePack
    {
        public string gacha_pack_id;
        public int quantity;
    }

    /// <summary>
    /// A single condition clause within a quest definition.
    /// </summary>
    [Serializable]
    public class QuestClause
    {
        public string clause_id;
        public string type;
        public QuestClauseItem[] items;
        public QuestClausePack packs;
    }

    /// <summary>
    /// The conditions block of a quest definition.
    /// </summary>
    [Serializable]
    public class QuestConditions
    {
        public string operator_type; // mapped manually; JSON key is "operator"
        public QuestClause[] clauses;
    }

    /// <summary>
    /// A single reward entry on a quest definition.
    /// </summary>
    [Serializable]
    public class QuestReward
    {
        public string reward_type;
        public int amount;
        public string item_definition_id;
        public int quantity_min;
        public int quantity_max;
    }

    /// <summary>
    /// The full quest definition embedded inside a chain member.
    /// </summary>
    [Serializable]
    public class QuestDefinitionData
    {
        public string id;
        public string studio_id;
        public string game_id;
        public string name;
        public string description;
        public string quest_type;
        public QuestConditions conditions;
        public QuestReward[] rewards;
        public bool is_active;
        public int sort_order;
        public string created_at;
        public string updated_at;
    }

    /// <summary>
    /// A single member entry inside a quest chain.
    /// </summary>
    [Serializable]
    public class ChainMemberData
    {
        public string id;
        public string chain_id;
        public string quest_definition_id;
        public int sort_order;
        public string[] unlock_quest_ids;
        public QuestDefinitionData definition;
        public string created_at;
        public string updated_at;
    }

    /// <summary>
    /// Response from GET /api/v1/games/{gameId}/quests/chains/{chainId}/members
    /// </summary>
    [Serializable]
    public class ChainMembersResponse
    {
        public ChainMemberData[] members;
    }

    // ── Chain Tree ────────────────────────────────────────────────────────────

    /// <summary>
    /// A single node in the quest chain tree.
    /// Note: JsonUtility supports shallow recursive arrays; deep trees
    /// may require a custom JSON parser (e.g. Newtonsoft.Json).
    /// </summary>
    [Serializable]
    public class QuestTreeNode
    {
        public string quest_id;
        public string quest_name;
        public string status;
        public QuestTreeNode[] children;
    }

    /// <summary>
    /// Response from GET /api/v1/games/{gameId}/quests/chains/{chainId}/tree
    /// </summary>
    [Serializable]
    public class ChainQuestTreeResponse
    {
        public string chain_id;
        public string chain_name;
        public QuestTreeNode[] nodes;
    }
}
