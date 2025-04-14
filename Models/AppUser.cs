using Microsoft.AspNetCore.Identity;

namespace CorpsAPI.Models
{
    public class AppUser : IdentityUser
    {
        public DateOnly DateOfBirth { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }
}
