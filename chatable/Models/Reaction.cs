using Postgrest.Attributes;
using Postgrest.Models;

namespace chatable.Models
{
    [Table("_reaction")]

    public class Reaction : BaseModel
    {
        [PrimaryKey("sender_id", true)]
        public string SenderId { get; set; }
        [PrimaryKey("message_id", true)]
        public string MessageId { get; set; }
        [Column("type")]
        public int Type { get; set; }
        
    }
}
