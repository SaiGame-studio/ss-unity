namespace SaiGame.Services
{
    /// <summary>
    /// A single node in the quest chain tree.
    /// Parsed with Newtonsoft.Json to support recursive children without Unity serialization limits.
    /// </summary>
    public class QuestTreeNode
    {
        public string quest_id;
        public string quest_name;
        public string status;
        public QuestTreeNode[] children;
    }
}
