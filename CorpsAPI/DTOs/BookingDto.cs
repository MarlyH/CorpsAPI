using CorpsAPI.Models;
using CorpsAPI.DTOs.Child;


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
        public string AttendeeName { get; set; } = default!;
        public DateOnly EventDate { get; set; }
        public int? SeatNumber { get; set; }
        public BookingStatus Status { get; set; }
        public bool IsForChild { get; set; }
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
        public string? PhoneNumber { get; set; } = null;
        public bool CanBeLeftAlone { get; set; }
        public string? ReservedBookingParentGuardianName { get; set;} = null;
    }
    // for use in scanning qrcode in app
    public class ScanQrCodeDto { public string QrCodeData { get; set; } = default!; }
    public class BookingIdDto { public int BookingId { get; set; } }

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

    public class AdminUserMiniDto
    {
        public string Id { get; set; } = default!;
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
        public bool HasMedicalConditions { get; set; }
        public List<MedicalConditionDto>? MedicalConditions { get; set; }
        public int AttendanceStrikeCount { get; set; }
        public DateOnly? DateOfLastStrike { get; set; }
        public bool IsSuspended { get; set; }
    }

    public class BookingScanDetailDto
    {
        // Booking + Event
        public int BookingId { get; set; }
        public int EventId { get; set; }
        public string? EventName { get; set; }          // Location.Name
        public DateOnly EventDate { get; set; }
        public string StartTime { get; set; } = default!; // "hh:mm"
        public string EndTime { get; set; } = default!;   // "hh:mm"
        public string SessionType { get; set; } = default!;
        public string? LocationName { get; set; }
        public string? Address { get; set; }

        // Booking fields
        public int? SeatNumber { get; set; }
        public BookingStatus Status { get; set; }
        public bool CanBeLeftAlone { get; set; }
        public string QrCodeData { get; set; } = default!;
        public bool IsForChild { get; set; }
        public string AttendeeName { get; set; } = default!;

        public ChildDto? Child { get; set; }// full ChildDto if IsForChild
        public AdminUserMiniDto? User { get; set; }// small user block for context
    }
}