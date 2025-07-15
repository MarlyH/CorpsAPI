using System.ComponentModel.DataAnnotations.Schema;

namespace CorpsAPI.Models
{
    public class UserDeviceToken
    {
        [ForeignKey("User")]
        public string UserId { get; set; } = default!;
        public AppUser? User { get; set; }
        public string Token { get; set; } = default!;
        public string Platform { get; set; } = default!;
    }
}
