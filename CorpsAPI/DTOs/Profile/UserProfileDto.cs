namespace CorpsAPI.DTOs.Profile
{
    public class UserProfileDto
    {
        public string UserName { get; set; } = default!;
        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
        public string Email { get; set; } = default!;
        public int Age { get; set; }
        public bool IsSuspended { get; set; } = false;
        public int AttendanceStrikeCount { get; set; }
        public string PhoneNumber { get; set; } = default!;
    }
}
