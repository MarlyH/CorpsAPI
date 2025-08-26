namespace CorpsAPI.DTOs.Profile
{
    public class BanAppealRequest
    {
        public string Email { get; set; } = default!;
    }

    public class BannedUserDto
    {
        public string Email { get; set; } = default!;
        public string FirstName { get; set; } = "";
        public string LastName  { get; set; } = "";
        public int AttendanceStrikeCount { get; set; }
        public DateOnly? DateOfLastStrike { get; set; }
        public DateTime? SuspensionUntil { get; set; }
    }
}
