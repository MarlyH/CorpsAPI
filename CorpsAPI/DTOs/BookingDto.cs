using CorpsAPI.Models;

namespace CorpsAPI.DTOs
{
    public class CreateBookingDto
    {
        public int EventId { get; set; }
        public int? SeatNumber { get; set; }
        public bool CanBeLeftAlone { get; set; } = false;
        public bool IsForChild { get; set; } = false;
        public int? ChildId { get; set; }
    }

    public class BookingResponseDto
    {
        public int BookingId { get; set; }
        public int EventId { get; set; }
        public string EventName { get; set; } = default!;
        public string AttendeeName { get; set; } = default!;  // ← new
        public DateOnly EventDate { get; set; }
        public int? SeatNumber { get; set; }
        public BookingStatus Status { get; set; }
        public bool CanBeLeftAlone { get; set; }
        public string QrCodeData { get; set; } = default!;
    }

    public class ManualStatusUpdateDto
    {
        public int BookingId { get; set; }
        public BookingStatus NewStatus { get; set; }
    }

    public class ReserveSeatDto
    {
        public int EventId { get; set; }
        public int SeatNumber { get; set; }
        public string? AttendeeName { get; set; } = null;
    }
    // for use in scanning qrcode in app
    public class ScanQrCodeDto { public string QrCodeData { get; set; } = default!; }
    public class BookingIdDto   { public int BookingId   { get; set; } }

    public class BookingScanInfoResponse
    {
        public int BookingId { get; set; }
        public int EventId { get; set; }
        public string AttendeeName { get; set; } = "";
        public string? SessionType { get; set; }
        public string? Date { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public int? SeatNumber { get; set; }
        public string Status { get; set; } = "";   // "Booked" | "CheckedIn" | "CheckedOut" | "Cancelled"
    }
}