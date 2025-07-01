using Microsoft.AspNetCore.Identity;

namespace CorpsAPI.Models
{
    public class AppUser : IdentityUser
    {
        public DateOnly DateOfBirth { get; set; }
        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
        public List<Booking> Bookings { get; set; } = new();
        public List<Event> ManagedEvents { get; set; } = new();
    }
}
