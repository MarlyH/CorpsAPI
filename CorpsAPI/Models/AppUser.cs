using System.ComponentModel.DataAnnotations.Schema;
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
        public List<Child> Children { get; set; } = new();
        public List<Waitlist> Waitlists { get; set; } = new();
        public List<UserDeviceToken> UserDeviceTokens { get; set; } = new();

        [NotMapped]
        public int Age 
        { 
            get
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                var age = today.Year - DateOfBirth.Year;

                if (today.Month < DateOfBirth.Month ||
                    (today.Month == DateOfBirth.Month && today.Day < DateOfBirth.Day))
                {
                    age--;
                }

                return age;
            } 
        }
    }
}
