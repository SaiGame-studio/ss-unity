using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Represents a single message in the mailbox.
    /// </summary>
    [Serializable]
    public class MailboxMessage
    {
        public string ID;
        public string SenderID;
        public string Subject;
        public string Body;
        public string MessageType;
        public string Status;
        public MailBoxAttachment[] Attachments;
        public string ExpiresAt;
        public string ReadAt;
        public string ClaimedAt;
        public string CreatedAt;
    }

    /// <summary>
    /// Represents an attachment in a mailbox message.
    /// </summary>
    [Serializable]
    public class MailBoxAttachment
    {
        public string AttachmentType;
        public string ItemDefinitionID;
        public int Quantity;
        public string ItemName;
        public int CoinAmount;
    }

    /// <summary>
    /// Represents the response from the mailbox messages API.
    /// </summary>
    [Serializable]
    public class MailBoxResponse
    {
        public MailboxMessage[] messages;
        public int total;
    }
}