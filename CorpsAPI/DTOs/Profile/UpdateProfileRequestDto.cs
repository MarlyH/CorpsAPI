namespace CorpsAPI.DTOs.Profile
{
    public class UpdateProfileRequestDto
    {
        public string? NewUserName { get; set; }
        public string? NewFirstName { get; set; }
        public string? NewLastName { get; set; }
        public string? NewPhoneNumber { get; set; }
    }
}
