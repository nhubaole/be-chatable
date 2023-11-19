using System.ComponentModel.DataAnnotations;

namespace chatable.Contacts.Requests
{
    public class MessageRequest
    {
        [Required]
        public string MessageType { get; set; }
        
        public string TextContent { get; set; }
        public string ImageContent { get; set; }
        public string AudioContent { get; set; }
    }
}
