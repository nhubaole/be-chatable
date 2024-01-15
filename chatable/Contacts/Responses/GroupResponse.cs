using chatable.Contacts.Responses;

namespace chatable.Contacts.Requests
{
    public class GroupDetailRequest
    {
        public string GroupId { get; set; }
        public string GroupName { get; set; }
        public string Avatar { get; set; }
        public string AdminId { get; set; }
        public List<MemberResponse> ListMember { get; set; }
    }
}
