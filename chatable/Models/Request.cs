using Postgrest.Attributes;
using Postgrest.Models;

namespace chatable.Models
{
    [Table("_request")]
    public class Request : BaseModel
    {
        [PrimaryKey("sender_id", true)]
        public string SenderId { get; set; }
        [PrimaryKey("receiver_id", true)]
        public string ReceiverId { get; set; }
        [Column("status")]
        public string Status { get; set; }
        [Column("sent_at")]
        public DateTime SentAt { get; set; }
    }
}
