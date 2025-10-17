public class UserMiniDto
{
    public string Id { get; set; } = default!;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}
public class UserSummaryDto
{
    public string Id { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string PhoneNumber { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string FullName => $"{FirstName} {LastName}".Trim();
    public List<string> Roles { get; set; } = new();
    public int AttendanceStrikeCount { get; set; }
    public DateOnly? DateOfLastStrike { get; set; }
    public bool IsSuspended { get; set; }
}

public class UsersByIdsRequest
{
    public List<string> Ids { get; set; } = new();
}
