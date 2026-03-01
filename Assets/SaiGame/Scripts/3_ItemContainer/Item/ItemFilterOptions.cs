using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Holds independent filter criteria for local item filtering.
    /// Each field is optional – empty/default means "no filter for this field".
    /// </summary>
    [Serializable]
    public class ItemFilterOptions
    {
        /// <summary>Case-insensitive substring match against item definition name.</summary>
        public string nameSearch = "";

        /// <summary>Exact match against item definition category (e.g. "weapon"). Empty = any.</summary>
        public string category = "";

        /// <summary>Exact match against item definition rarity (e.g. "common"). Empty = any.</summary>
        public string rarity = "";

        /// <summary>When true, only stackable items are included.</summary>
        public bool stackableOnly = false;

        /// <summary>Returns true when no filter criteria are active.</summary>
        public bool IsEmpty =>
            string.IsNullOrEmpty(this.nameSearch) &&
            string.IsNullOrEmpty(this.category) &&
            string.IsNullOrEmpty(this.rarity) &&
            !this.stackableOnly;

        public void Clear()
        {
            this.nameSearch  = "";
            this.category    = "";
            this.rarity      = "";
            this.stackableOnly = false;
        }
    }
}
