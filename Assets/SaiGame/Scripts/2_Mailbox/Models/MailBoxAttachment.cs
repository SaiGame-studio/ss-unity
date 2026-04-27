using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Represents an attachment in a mailbox message.
    /// </summary>
    [Serializable]
    public class MailBoxAttachment
    {
        public string type;
        public string definition_id;
        public int quantity;
        public ItemDefinitionData item_definition;
    }
}
