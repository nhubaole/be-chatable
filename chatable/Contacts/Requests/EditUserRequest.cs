using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace chatable.Contacts.Requests
{
    public class EditUserRequest
    {
        public string? FullName { get; set; }
        public DateTime? DOB { get; set; }
        public string? Gender { get; set; }
    }
}
