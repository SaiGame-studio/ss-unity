using System;

namespace SaiGame.Services
{
    [Serializable]
    public class ItemTagsResponse
    {
        public int limit;
        public int offset;
        public ItemTagData[] tags;
        public int total;
    }
}
