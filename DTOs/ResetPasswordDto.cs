namespace CorpsAPI.DTOs
{
    public class ResetPasswordDto
    {
        public string Email { get; set; }
        public string ResetPasswordToken { get; set; }
        public string NewPassword { get; set; }
    }
}
