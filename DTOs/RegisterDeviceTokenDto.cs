using System.ComponentModel.DataAnnotations;

namespace CorpsAPI.DTOs
{
    public class RegisterDeviceTokenDto
    {
        [Required]
        public string DeviceToken { get; set; } = default!;
        [Required]
        [RegularExpression("iOS|Android", ErrorMessage = "Platform must be either 'iOS' or 'Android'.")]
        public string Platform { get; set; } = default!;
    }
}
