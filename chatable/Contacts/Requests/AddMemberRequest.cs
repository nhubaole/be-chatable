namespace chatable.Contacts.Requests
{
    public class AddMemberRequest
    {
        public string GroupId { get; set; }
        public List<string> MemberList { get; set; }
    }
}
