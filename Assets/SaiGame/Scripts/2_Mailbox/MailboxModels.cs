using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Request model for reading a message (currently empty as per API).
    /// </summary>
    [Serializable]
    public class ReadMessageRequest
    {
        // Empty request body for read operation
    }

    /// <summary>
    /// Request model for claiming a message (currently empty as per API).
    /// </summary>
    [Serializable]
    public class ClaimMessageRequest
    {
        // Empty request body for claim operation
    }

    /// <summary>
    /// Response model for read message operation.
    /// </summary>
    [Serializable]
    public class ReadMessageResponse
    {
        public MailboxMessage message;
        public string message_text;
    }

    /// <summary>
    /// A single reward item returned after claiming a message.
    /// </summary>
    [Serializable]
    public class ClaimReward
    {
        public string type;
        public string definition_id;
        public int quantity;
    }

    /// <summary>
    /// Response model for claim message operation.
    /// </summary>
    [Serializable]
    public class ClaimMessageResponse
    {
        public ClaimReward[] rewards;
    }
}