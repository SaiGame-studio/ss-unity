using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Cursor-paginated response from GET /api/v1/games/{gameId}/quest-claims
    /// </summary>
    [Serializable]
    public class QuestClaimsResponse
    {
        public QuestClaimRecord[] claims;
        public int limit;
        public bool has_more;
        public string next_after;
    }
}
