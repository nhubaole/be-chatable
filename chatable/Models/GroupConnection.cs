using Postgrest.Attributes;
using Postgrest.Models;

namespace chatable.Models
{
    [Table("_group_connections")]
    public class GroupConnection : BaseModel
    {
        [PrimaryKey("connection_id", true)]
        public string ConnectionId { get; set; }

        [Column("group_id")]
        public string GroupId { get; set; }
    }
}