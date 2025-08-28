using CorpsAPI.Data;
using CorpsAPI.DTOs.Child;
using CorpsAPI.Models;
using CorpsAPI.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Route("api/[controller]")]
[ApiController]
public class BookingAdminController : ControllerBase
{
    private readonly AppDbContext _context;

    public BookingAdminController(AppDbContext context)
    {
        _context = context;
    }

    // GET: /api/BookingAdmin/detail/{bookingId}
    // Returns booking + (optional) child dto + light user info for admins/event managers.
    [HttpGet("detail/{bookingId:int}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager}")]
    public async Task<IActionResult> GetBookingDetailForAdmin(int bookingId)
    {
        var b = await _context.Bookings
            .AsNoTracking()
            .Include(x => x.Event).ThenInclude(e => e.Location)
            .Include(x => x.User)
            .Include(x => x.Child)
            .FirstOrDefaultAsync(x => x.BookingId == bookingId);

        if (b == null)
            return NotFound(new { message = "Booking not found." });

        var childDto = b.IsForChild && b.Child != null
            ? new ChildDto
            {
                ChildId = b.Child.ChildId,
                FirstName = b.Child.FirstName,
                LastName = b.Child.LastName,
                DateOfBirth = b.Child.DateOfBirth,
                EmergencyContactName = b.Child.EmergencyContactName,
                EmergencyContactPhone = b.Child.EmergencyContactPhone,
                Age = CalculateAge(b.Child.DateOfBirth)
            }
            : null;

        var userMini = b.User == null ? null : new
        {
            id = b.User.Id,
            email = b.User.Email,
            firstName = b.User.FirstName,
            lastName = b.User.LastName,
            attendanceStrikeCount = b.User.AttendanceStrikeCount,
            dateOfLastStrike = b.User.DateOfLastStrike,
            isSuspended = b.User.IsSuspended
        };

        var dto = new
        {
            bookingId = b.BookingId,
            eventId = b.EventId,
            eventName = b.Event?.Location?.Name,
            eventDate = b.Event?.StartDate,
            seatNumber = b.SeatNumber,
            status = b.Status,
            canBeLeftAlone = b.CanBeLeftAlone,
            qrCodeData = b.QrCodeData,
            isForChild = b.IsForChild,
            attendeeName = b.IsForChild
                ? (b.Child != null
                    ? $"{b.Child.FirstName} {b.Child.LastName}"
                    : (b.ReservedBookingAttendeeName ?? "Child"))
                : (b.User != null
                    ? $"{b.User.FirstName} {b.User.LastName}"
                    : "User"),
            user = userMini,
            child = childDto
        };

        return Ok(dto);
    }

    private static int CalculateAge(DateOnly dob)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - dob.Year;
        if (today.Month < dob.Month || (today.Month == dob.Month && today.Day < dob.Day))
            age--;
        return age;
    }
}
