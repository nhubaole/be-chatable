using Postgrest.Attributes;

namespace chatable.Contacts.Responses
{
    public class RequestResponse
    {
        public string SenderId { get; set; }
        public string ReceiverId { get; set; }
        public string Status { get; set; }
        public DateTime SentAt { get; set; }
    }
}
