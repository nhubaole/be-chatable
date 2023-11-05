using System.ComponentModel.DataAnnotations;

namespace chatable.Contacts.Requests
{
    public class UserLoginRequest
    {
        [Required]
        public string UserName { get; set; }
        [Required]
        public string Password { get; set; }
    }
}