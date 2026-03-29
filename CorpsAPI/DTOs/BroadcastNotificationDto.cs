using System.ComponentModel.DataAnnotations;

namespace CorpsAPI.DTOs
{
    public class BroadcastNotificationDto
    {
        [Required]
        [StringLength(120, ErrorMessage = "Title must be 120 characters or fewer.")]
        public string Title { get; set; } = "Your Corps Update";

        [Required]
        [StringLength(500, ErrorMessage = "Message must be 500 characters or fewer.")]
        public string Message { get; set; } = string.Empty;
    }
}
