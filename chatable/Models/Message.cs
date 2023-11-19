using Postgrest.Attributes;
using Postgrest.Models;

namespace chatable.Models
{
    [Table("_message")]
    public class Message : BaseModel
    {
        [PrimaryKey("message_id", false)]
        public int MessageId { get; set; }

        [Column("sender_id")]
        public string SenderId { get; set; }

        [Column("conversation_id")]
        public string ConversationId { get; set; }

        [Column("sent_at")]
        public DateTime SentAt { get; set; }

        [Column("message_type")]
        public string MessageType { get; set; }

        [Column("text_content")]
        public string TextContent { get; set; }

        [Column("image_content")]
        public string ImageContent { get; set; }

        [Column("audio_content")]
        public string AudioContent { get; set; }
    }
}