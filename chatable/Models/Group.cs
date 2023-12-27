using Postgrest.Attributes;
using Postgrest.Models;
namespace chatable.Models
{
    [Table("_group")]
    public class Group : BaseModel
    {
        [PrimaryKey("group_id", true)]
        public string GroupId { get; set; }
        [Column("group_name")]
        public string GroupName { get; set; }
        [Column("owner_id")]
        public string AdminId { get; set; }
        [Column("avatar")]
        public string Avatar { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
