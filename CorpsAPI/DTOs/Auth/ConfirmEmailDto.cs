
namespace CorpsAPI.DTOs.Auth
{
    public class ConfirmEmailDto
    {
        public string UserId { get; set; } = default!;
        public string Token { get; set; } = default!;
    }
}