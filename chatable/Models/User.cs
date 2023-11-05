using Postgrest;
using Postgrest.Attributes;
using Postgrest.Models;

namespace chatable.Models
{

    [Table("user")]
    public class User : BaseModel
    {
        [PrimaryKey("user_name", true)]
        public string? UserName { get; set; }
        [Column("full_name")]
        public string FullName { get; set; }
        [Column("password")]
        public string Password { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
