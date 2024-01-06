namespace chatable.Contacts.Responses
{
    public class ProfileUser
    {
        public string? UserName { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string AvatarUrl { get; set; }
        public DateTime DOB { get; set; }
        public string Gender { get; set; }
        public bool isFriend { get; set; }

    }
}
