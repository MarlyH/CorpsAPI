using CorpsAPI.Models;
using System.ComponentModel.DataAnnotations;

namespace CorpsAPI.DTOs
{
    public class UpdateAttendanceDto
    {
        [Required]
        public int BookingId { get; set; }

        [Required]
        public BookingStatus NewStatus { get; set; }
    }
}