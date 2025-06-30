using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CorpsAPI.Models
{
    public class Booking
    {
        [Key]
        public int BookingId { get; set; }
        public int EventId { get; set; }
        [ForeignKey("EventId")]
        public Event? Event { get; set; }
        public string? AttendingUserId { get; set; } // nullable in case user is deleted. We want to retain booking records
        [ForeignKey("AttendingUserId")]
        public AppUser? AttendingUser { get; set; }
        [Required]
        public int SeatNumber { get; set; }
        public BookingStatus Status { get; set; }
        public bool CanBeLeftAlone { get; set; } = false;
        public string QrCodeData { get; set; } = default!;
    }

    public enum BookingStatus
    {
        Booked,
        CheckedIn,
        CheckedOut
    }
}
