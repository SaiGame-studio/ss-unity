using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Filter options for local mailbox message filtering.
    /// </summary>
    public enum MailboxStatusFilter
    {
        All,
        Unread,
        Read,
        Claimed
    }

    /// <summary>
    /// Represents a single message in the mailbox.
    /// </summary>
    [Serializable]
    public class MailboxMessage
    {
        public string id;
        public string sender_id;
        public string subject;
        public string body;
        public string message_type;
        public string status;
        public MailBoxAttachment[] attachments;
        public string expires_at;
        public string read_at;
        public string claimed_at;
        public string created_at;
    }

    /// <summary>
    /// Represents an attachment in a mailbox message.
    /// </summary>
    [Serializable]
    public class MailBoxAttachment
    {
        public string type;
        public string definition_id;
        public int quantity;
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