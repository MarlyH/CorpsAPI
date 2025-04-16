namespace CorpsAPI.DTOs
{
    public class ResetPasswordDto
    {
        public string Email { get; set; }
        public string ResetPswdToken { get; set; }
        public string NewPswd { get; set; }
    }
}
