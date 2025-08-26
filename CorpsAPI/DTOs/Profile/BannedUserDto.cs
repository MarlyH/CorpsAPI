public sealed class BannedUserDto
{
    public string Id { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public int AttendanceStrikeCount { get; set; }
    public DateOnly? DateOfLastStrike { get; set; }
    public DateTime? SuspensionUntil { get; set; }
}
