namespace CorpsAPI.DTOs.PasswordRecovery
{
    public class ResetPasswordDto
    {
        public string Email { get; set; } = default!;
        public string ResetPasswordToken { get; set; } = default!;
        public string NewPassword { get; set; } = default!;
    }
}
