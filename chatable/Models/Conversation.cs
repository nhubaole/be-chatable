using Postgrest.Attributes;
using Postgrest.Models;

namespace chatable.Models
{
    [Table("_conversation")]
    public class Conversation : BaseModel
    {
        [PrimaryKey("conversation_id", true)]
        public string ConversationId { get; set; }

        [Column("conversation_type")]
        public string ConversationType { get; set; }

        [Column("last_message")]
        public int LastMessage { get; set; }

        [Column("unread_message_count")]
        public int UnreadMessageCount { get; set; }

    }
}
