using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CorpsAPI.Models
{
    public class Event
    {
        [Key]
        public int EventId { get; set; }
        public int? LocationId { get; set; }
        [ForeignKey("LocationId")]
        public Location? Location { get; set; }
        public string? EventManagerId { get; set; } = default!;
        [ForeignKey("EventManagerId")]
        public AppUser? EventManager { get; set; }
        [Required]
        public EventCategory Category { get; set; } = EventCategory.Bookable;
        [Required]
        public bool RequiresBooking { get; set; } = true;
        [MaxLength(160)]
        public string? Title { get; set; }
        [Required]
        public EventSessionType SessionType { get; set; }
        [Required]
        public DateOnly StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        [Required]
        public TimeOnly StartTime { get; set; }
        [Required]
        public TimeOnly EndTime { get; set; }
        [Required]
        public DateOnly AvailableDate { get; set; }
        public string? SeatingMapImgSrc { get; set; }
        public string? EventImageImgSrc { get; set; }
        [Required]
        public int TotalSeats { get; set; }
        public List<Booking> Bookings { get; set; } = new();
        public List<Waitlist> Waitlists { get; set; } = new();
        [NotMapped]
        public int AvailableSeats { get { return TotalSeats - (Bookings?.Count(b => b.Status != BookingStatus.Cancelled) ?? 0); } }
        // [MaxLength(500)]
        public string? Description { get; set; }
        [MaxLength(100)]
        public string? Address { get; set; }
        [Required]
        public EventStatus Status { get; set; }
        [Required]
        public bool IsReminded { get; set; } = false;
        [NotMapped]
        public int AttendanceCount { get { return Bookings?.Count(b => b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.CheckedOut) ?? 0; } }
    }

    public enum EventSessionType
    {
        Kids,
        Teens,
        Adults
    }

    public enum EventCategory
    {
        Bookable = 0,
        Announcement = 1,
        Promotional = 2
    }

    public enum EventStatus
    {
        // The event is not yet available for booking. 
        // It will be updated to 'Available' by a nightly Azure Function based on the 'AvailableDate' field.
        Unavailable,

        // The event is open for booking. 
        // It will be updated to 'Concluded' by a nightly Azure Function once the event's StartDate and EndTime have passed.
        Available,

        // The event has already taken place.
        Concluded,

        // The event has been cancelled manually and will not proceed.
        Cancelled
    }
}
