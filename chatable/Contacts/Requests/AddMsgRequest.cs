namespace chatable.Contacts.Requests
{
    public class AddMsgRequest
    {
        public string ConversationId { get; set; }
        public string ConversationType { get; set; }
        public string MessageType { get; set; }
    }
}
