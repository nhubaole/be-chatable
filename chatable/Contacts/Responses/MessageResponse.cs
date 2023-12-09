using System.ComponentModel.DataAnnotations;

namespace chatable.Contacts.Responses
{
    public class MessageResponse
    {
        [Required]
        public string SenderId { get; set; }
        [Required]
        public string MessageType { get; set; }
        [Required]
        public string Content { get; set; }
        public DateTime SentAt { get; set; }
    }
}
