using Postgrest;
using Postgrest.Attributes;
using Postgrest.Models;

namespace chatable.Models
{

    [Table("_user")]
    public class User : BaseModel
    {
        [PrimaryKey("username", true)]
        public string? UserName { get; set; }
        [Column("full_name")]
        public string FullName { get; set; }
        [Column("avatar")]
        public string Avatar { get; set; }
        [Column("dob")]
        public DateTime DOB { get; set; }

        [Column("gender")]
        public string Gender { get; set; }

        [Column("password")]
        public string Password { get; set; }
        [Column("last_time_online")]
        public DateTime LastTimeOnl { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
