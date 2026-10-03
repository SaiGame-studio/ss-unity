namespace SaiGame.Services
{
    /// <summary>
    /// Response from GET /api/v1/games/{gameId}/quests/chains/{chainId}/tree
    /// </summary>
    public class ChainQuestTreeResponse
    {
        public string chain_id;
        public string chain_name;
        public QuestTreeNode[] nodes;
    }
}
