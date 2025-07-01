using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CorpsAPI.Models
{
    public class Event
    {
        [Key]
        public int EventId { get; set; }
        [Required]
        public int LocationId { get; set; }
        [ForeignKey("LocationId")]
        public Location? Location { get; set; }
        public string EventManagerId { get; set; } = default!;
        [ForeignKey("EventManagerId")]
        public AppUser? EventManager { get; set; }
        [Required]
        public EventSessionType SessionType { get; set; }
        [Required]
        public DateOnly StartDate { get; set; }
        [Required]
        public TimeOnly StartTime { get; set; }
        [Required]
        public TimeOnly EndTime { get; set; }
        [Required]
        public DateOnly AvailableDate { get; set; }
        public string SeatingMapImgSrc { get; set; } = default!;
        public int TotalSeats { get; set; }
        public List<Booking> Bookings { get; set; } = new();
        [NotMapped]
        public int AvailableSeats { get { return TotalSeats - (Bookings?.Count ?? 0); } }
        [MaxLength(500)]
        public string? Description { get; set; }
        [MaxLength(100)]
        public string? Address { get; set; }
        [NotMapped]
        public int AttendanceCount { get { return Bookings?.Count(b => b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.CheckedOut) ?? 0; } }
    }

    public enum EventSessionType
    {
        Kids,
        Teens,
        Adults
    }
}
