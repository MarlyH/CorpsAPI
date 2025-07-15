namespace CorpsAPI.DTOs.Profile
{
    public class ChangeUserRole
    {
        public string Email { get; set; } = default!;
        public string Role { get; set; } = default!;
    }
}
