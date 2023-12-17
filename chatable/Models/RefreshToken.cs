using Postgrest.Attributes;
using Postgrest.Models;

namespace chatable.Models
{
    [Table("_refresh_token")]
    public class RefreshToken : BaseModel
    {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; }
        [Column("username")]
        public int UserId { get; set; }
        [Column("token")]
        public string Token { get; set; }
        [Column("jwt_id")]
        public string JwtId { get; set; }
        [Column("is_used")]
        public bool IsUsed { get; set; }
        [Column("is_revoked")]
        public bool IsRevoked { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        [Column("expired_at")]
        public DateTime ExpiredAt { get; set; }
    }
}
