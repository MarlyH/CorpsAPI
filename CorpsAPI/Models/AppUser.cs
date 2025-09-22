using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using CorpsAPI.Models;

namespace CorpsAPI.Models
{
    public class AppUser : IdentityUser
    {
        public DateOnly DateOfBirth { get; set; }
        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
        public int AttendanceStrikeCount { get; set; }
        public DateOnly? DateOfLastStrike { get; set; }
        public List<Booking> Bookings { get; set; } = new();
        public List<Event> ManagedEvents { get; set; } = new();
        public List<Child> Children { get; set; } = new();
        public List<Waitlist> Waitlists { get; set; } = new();
        public new string PhoneNumber { get; set; } = default!;
        public bool HasMedicalConditions { get; set; }
        public List<UserMedicalCondition> MedicalConditions { get; set; } = new();

        [NotMapped]
        public bool IsSuspended
        {
            get
            {
                // once the user has been suspended, we reset their strike count after 90 days.
                // strikes are NOT reset until a user gets suspended.
                if (AttendanceStrikeCount >= 3 && DateOfLastStrike.HasValue)
                {
                    var daysSinceLastStrike = DateTime.Today - DateOfLastStrike.Value.ToDateTime(TimeOnly.MinValue);
                    if (daysSinceLastStrike.TotalDays > 90)
                    {
                        AttendanceStrikeCount = 0;
                        DateOfLastStrike = null;
                    }
                }

                return AttendanceStrikeCount >= 3;
            }
        }
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
