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
        public string? UserId { get; set; } // nullable in case user is deleted. We want to retain booking records
        [ForeignKey("UserId")]
        public AppUser? User { get; set; }
        public int? SeatNumber { get; set; } // nullable for when cancelling to prevent "seat already taken" errors
        public BookingStatus Status { get; set; }
        public bool CanBeLeftAlone { get; set; } = false;
        public string QrCodeData { get; set; } = default!;
        public bool IsForChild { get; set; } = false;
        public int? ChildId { get; set; } // not all bookings will be for a child
        [ForeignKey("ChildId")]
        public Child? Child { get; set; }
        public string? ReservedBookingAttendeeName { get; set; } = null;
    }

    public enum BookingStatus
    {
        Booked,
        CheckedIn,
        CheckedOut,
        Cancelled
    }
}
