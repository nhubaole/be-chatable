using Postgrest.Attributes;
using Postgrest.Models;

namespace chatable.Models
{
    [Table("_user_connections")]
    public class Connection : BaseModel
    {
        [PrimaryKey("connection_id", true)]
        public string ConnectionId { get; set; }

        [Column("user_id")]
        public string UserId { get; set; }

        [Column("online_status")]
        public string OnlineStatus { get; set; }
    }
}