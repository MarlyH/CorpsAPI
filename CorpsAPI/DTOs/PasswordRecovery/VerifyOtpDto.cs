namespace CorpsAPI.DTOs.PasswordRecovery
{
    public class VerifyOtpDto
    {
        public string Otp { get; set; } = default!;
        public string Email { get; set; } = default!;
    }
}
