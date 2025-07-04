using CorpsAPI.Models;

namespace CorpsAPI.DTOs
{
    public class CreateBookingDto
    {
        public int EventId { get; set; }
        public int SeatNumber { get; set; }
        public bool CanBeLeftAlone { get; set; } = false;
        public bool IsForChild { get; set; } = false;
        public int? ChildId { get; set; }
    }

    public class BookingResponseDto
    {
        public int BookingId { get; set; }
        public int EventId { get; set; }
        public string EventName { get; set; } = default!;
        public DateOnly EventDate { get; set; }
        public int SeatNumber { get; set; }
        public BookingStatus Status { get; set; }
        public bool CanBeLeftAlone { get; set; }
        public string QrCodeData { get; set; } = default!;
    }

    public class ScanQrCodeDto
    {
        public string QrCodeData { get; set; } = default!;
    }
}