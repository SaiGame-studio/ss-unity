using System.Collections.Generic;
using UnityEngine;

namespace SaiGame.Services
{
    /// <summary>
    /// Runtime cache of item definitions received from item APIs.
    /// The serialized list is intentionally kept for inspection while the game is running.
    /// </summary>
    public class ItemsCached : SaiBehaviour
    {
        [SerializeField] private List<ItemDefinitionData> itemDefinitions = new List<ItemDefinitionData>();

        /// <summary>Item definitions currently stored in this cache.</summary>
        public IReadOnlyList<ItemDefinitionData> ItemDefinitions => this.itemDefinitions;

        /// <summary>Adds a definition to the cache, or replaces its existing entry with the same ID.</summary>
        public void Cache(ItemDefinitionData itemDefinition)
        {
            if (itemDefinition == null || string.IsNullOrEmpty(itemDefinition.id))
                return;

            for (int i = 0; i < this.itemDefinitions.Count; i++)
            {
                if (this.itemDefinitions[i] != null && this.itemDefinitions[i].id == itemDefinition.id)
                {
                    this.itemDefinitions[i] = itemDefinition;
                    return;
                }
            }

            this.itemDefinitions.Add(itemDefinition);
        }

        /// <summary>Adds or updates all non-null definitions in the supplied collection.</summary>
        public void CacheRange(IEnumerable<ItemDefinitionData> definitions)
        {
            if (definitions == null)
                return;

            foreach (ItemDefinitionData definition in definitions)
                this.Cache(definition);
        }

        /// <summary>Returns a cached item definition by its ID, or null when it is not cached.</summary>
        public ItemDefinitionData GetItemById(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return null;

            for (int i = 0; i < this.itemDefinitions.Count; i++)
            {
                ItemDefinitionData definition = this.itemDefinitions[i];
                if (definition != null && definition.id == itemId)
                    return definition;
            }

            return null;
        }

        /// <summary>Returns a cached item definition by its item code, or null when it is not cached.</summary>
        public ItemDefinitionData GetItemByCode(string itemCode)
        {
            if (string.IsNullOrEmpty(itemCode))
                return null;

            for (int i = 0; i < this.itemDefinitions.Count; i++)
            {
                ItemDefinitionData definition = this.itemDefinitions[i];
                if (definition != null && definition.item_code == itemCode)
                    return definition;
            }

            return null;
        }

        /// <summary>Alias for <see cref="GetItemByCode"/> with an explicit item-code name.</summary>
        public ItemDefinitionData GetItemByItemCode(string itemCode)
        {
            return this.GetItemByCode(itemCode);
        }

        /// <summary>Removes all cached item definitions.</summary>
        public void Clear()
        {
            this.itemDefinitions.Clear();
        }
    }
}
