using chatable.Models;

namespace chatable.Contacts.Requests
{
    public class CreateGroupRequest
    {
        public string GroupName { get; set; }
        public List<string> MemberList { get; set; }
    }
}
