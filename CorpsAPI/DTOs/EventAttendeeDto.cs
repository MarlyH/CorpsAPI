using CorpsAPI.Models;

namespace CorpsAPI.DTOs
{
    public class EventAttendeeDto
    {
        public int BookingId { get; set; }
        public string Name { get; set; } = "";
        public BookingStatus Status { get; set; }
        public int? SeatNumber { get; set; }
        public bool IsForChild { get; set; }
    }
}