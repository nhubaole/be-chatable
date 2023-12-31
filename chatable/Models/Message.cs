using Postgrest.Attributes;
using Postgrest.Models;

namespace chatable.Models
{
    [Table("_message")]
    public class Message : BaseModel
    {
        [PrimaryKey("message_id", true)]
        public Guid MessageId { get; set; }

        [Column("sender_id")]
        public string SenderId { get; set; }

        [Column("conversation_id")]
        public string ConversationId { get; set; }

        [Column("sent_at")]
        public DateTime SentAt { get; set; }

        [Column("message_type")]
        public string MessageType { get; set; }

        [Column("content")]
        public string Content { get; set; }
    }
}