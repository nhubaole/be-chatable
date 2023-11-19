using Postgrest.Attributes;
using Postgrest.Models;

namespace chatable.Models
{
    [Table("_friend")]
    public class Friend : BaseModel
    {
        [PrimaryKey("user_id", true)]
        public string UserId { get; set; }
        [PrimaryKey("friend_id", true)]
        public string FriendId { get; set; }
    }
}