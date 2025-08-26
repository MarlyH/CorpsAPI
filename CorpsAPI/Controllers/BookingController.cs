using CorpsAPI.DTOs;
using CorpsAPI.Models;
using CorpsAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CorpsAPI.Constants;
using System.Data;
using CorpsAPI.Data;

namespace CorpsAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BookingController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly EmailService _emailService;
        private readonly NotificationService _notificationService;

        public BookingController(AppDbContext context, UserManager<AppUser> userManager, EmailService emailService, NotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
            _notificationService = notificationService;
        }

        [HttpPost]
        [Authorize(Roles = $"{Roles.User},{Roles.Admin},{Roles.EventManager},{Roles.Staff}")]
        public async Task<IActionResult> CreateBooking([FromBody] CreateBookingDto dto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            var eventEntity = await _context.Events
                .Include(e => e.Bookings)
                .Include(e => e.Location)
                .FirstOrDefaultAsync(e => e.EventId == dto.EventId);

            if (eventEntity == null || eventEntity.Status != EventStatus.Available)
                return NotFound(new { message = "Event not found or not available for booking." });

            // only treat seats on non‑cancelled bookings as “taken”
            if (eventEntity.Bookings.Any(b =>
                    b.SeatNumber == dto.SeatNumber
                && b.Status     != BookingStatus.Cancelled))
            {
                return BadRequest(new { message = "That seat is already taken." });
            }

            if (dto.SeatNumber < 1 || dto.SeatNumber > eventEntity.TotalSeats)
                return BadRequest(new { message = "Seat out of bounds." });

            if (eventEntity.AvailableSeats <= 0)
                return BadRequest(new { message = "No seats available." });

            int bookingAge;
            if (dto.IsForChild)
            {
                // only block children with *active* bookings
                if (eventEntity.Bookings.Any(b =>
                        b.ChildId == dto.ChildId
                    && b.Status  != BookingStatus.Cancelled))
                {
                    return BadRequest(new { message = "This child already has a booking for this event." });
                }

                var child = await _context.Children.FindAsync(dto.ChildId);
                if (child == null)
                    return BadRequest(new { message = "Booking is for child but child ID not found." });

                bookingAge = child.Age;
            }
            else
            {
                // only block users with *active* bookings
                if (eventEntity.Bookings.Any(b =>
                        b.UserId == user.Id
                    && b.Status != BookingStatus.Cancelled))
                {
                    return BadRequest(new { message = "You already have an active booking for this event." });
                }

                bookingAge = user.Age;
            }

            bool AgeIsOk = false;

            switch (eventEntity.SessionType)
            {
                case EventSessionType.Kids:
                    if (bookingAge >= 5 && bookingAge < 12)
                        AgeIsOk = true;
                    break;

                case EventSessionType.Teens:
                    if (bookingAge >= 12 && bookingAge < 16)
                        AgeIsOk = true;
                    break;

                case EventSessionType.Adults:
                    if (bookingAge >= 16)
                        AgeIsOk = true;
                    break;

                default:
                    return BadRequest(new { message = ErrorMessages.InternalServerError });
            }

            if (!AgeIsOk)
                return BadRequest(new { message = "Provided age for the booking is not within bounds." });

            var booking = new Booking
            {
                EventId = dto.EventId,
                SeatNumber = dto.SeatNumber,
                CanBeLeftAlone = dto.CanBeLeftAlone,
                UserId = user.Id,
                Status = BookingStatus.Booked,
                QrCodeData = Guid.NewGuid().ToString(),
                IsForChild = dto.IsForChild,
                ChildId = dto.ChildId
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            // send confirmation email
            var emailBody = $@"
                <p>Dear {user.UserName},</p>

                <p>Thank you for your booking. Your reservation for the event below has been successfully confirmed:</p>

                <ul>
                  <li><strong>Date:</strong> {eventEntity.StartDate:dddd, MMMM d, yyyy}</li>
                  <li><strong>Time:</strong> {eventEntity.StartTime:h:mm} - {eventEntity.EndTime:h:mm}</li>
                  <li><strong>Location:</strong> {eventEntity.Location?.Name ?? "TBA"}, {eventEntity.Address ?? "No address provided"}</li>
                  <li><strong>Seat Number:</strong> {dto.SeatNumber}</li>
                  <li><strong>Session Type:</strong> {eventEntity.SessionType}</li>
                </ul>

                <p>A QR code for entry is now linked to your booking. You can access it in the app to present it at the event entrance for checking in and out of the event.</p>

                <p>We look forward to seeing you there!</p>

                <p>Warm regards,<br />
                The Your Corps Team</p>
                ";

            // send email in background so we don't slow down the response
            _ = Task.Run(async () =>
            {
                try
                {
                    await _emailService.SendEmailAsync(user.Email!, "Booking Confirmation", emailBody);
                }
                catch 
                { 
                    // TODO: implement logging
                }
            });

            return Ok(new { message = "Booking created successfully" });
        }

        [HttpGet("my")]
        [Authorize]
        public async Task<IActionResult> GetMyBookings()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var bookings = await _context.Bookings
                .AsNoTracking()
                .Include(b => b.Event).ThenInclude(e => e.Location)
                .Include(b => b.Child)
                .Include(b => b.User)
                // drop child-bookings whose Child was deleted
                .Where(b => b.UserId == userId && (!b.IsForChild || b.ChildId != null))
                .Select(b => new BookingResponseDto
                {
                    BookingId = b.BookingId,
                    EventId = b.EventId,
                    EventName = b.Event.Location!.Name,
                    EventDate = b.Event.StartDate,
                    SeatNumber = b.SeatNumber,
                    Status = b.Status,
                    CanBeLeftAlone = b.CanBeLeftAlone,
                    QrCodeData = b.QrCodeData,
                    AttendeeName = b.IsForChild
                        ? (b.Child != null
                            ? $"{b.Child.FirstName} {b.Child.LastName}"
                            : (b.ReservedBookingAttendeeName ?? "Child"))
                        : (b.User != null
                            ? $"{b.User.FirstName} {b.User.LastName}"
                            : "User")
                })
                .ToListAsync();

            return Ok(bookings);
        }

        [HttpPut("cancel/{id}")]
        [Authorize]
        public async Task<IActionResult> CancelBooking(int id, [FromServices] EmailService emailService)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var booking = await _context.Bookings
                .Include(b => b.Event)
                    .ThenInclude(e => e.Location)
                .FirstOrDefaultAsync(b => b.BookingId == id);

            if (booking == null || booking.UserId != userId)
                return NotFound(new { message = "Booking not found." });

            var ev = booking.Event!;
            if (ev.Status == EventStatus.Concluded || ev.Status == EventStatus.Cancelled)
                return BadRequest(new { message = "You cannot cancel a booking after the event has concluded." });

            // count active before we cancel
            var activeBefore = await _context.Bookings.CountAsync(b =>
                b.EventId == ev.EventId &&
                b.Status   != BookingStatus.Cancelled &&
                b.SeatNumber != null);
            var wasFull = activeBefore >= ev.TotalSeats;

            // cancel + free seat
            booking.Status = BookingStatus.Cancelled;
            booking.SeatNumber = null;
            await _context.SaveChangesAsync();

            if (wasFull)
            {
                var waitlistEntries = await _context.Waitlists
                    .Where(w => w.EventId == ev.EventId)
                    .ToListAsync();

                // Build "where" + "when" strings for waitlist notifs
                var venueName = ev.Location?.Name;
                var where = !string.IsNullOrWhiteSpace(venueName)
                    ? (!string.IsNullOrWhiteSpace(ev.Address) ? $"{venueName}, {ev.Address}" : venueName)
                    : (ev.Address ?? "TBA");

                var when = $"{ev.StartDate:ddd, MMM d} at {ev.StartTime:hh\\:mm}";

                foreach (var entry in waitlistEntries)
                {
                    await _notificationService.SendCrossPlatformNotificationAsync(
                        entry.UserId,
                        "Seat available!",
                        $"A seat just opened at {where} on {when}.");
                    _context.Remove(entry);
                }

                await _context.SaveChangesAsync();
            }
            return Ok(new { message = "Booking successfully cancelled." });
        }

        //this is a depreciated scan in/out toggle 

        // [HttpPost("scan")]
        // [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager},{Roles.Staff}")]
        // public async Task<IActionResult> ScanQrCode([FromBody] ScanQrCodeDto dto)
        // {
        //     var booking = await _context.Bookings
        //         .Include(b => b.Event)
        //         .FirstOrDefaultAsync(b => b.QrCodeData == dto.QrCodeData);

        //     if (booking == null)
        //         return NotFound(new { message = "Invalid QR code." });

        //     switch (booking.Status)
        //     {
        //         case BookingStatus.Booked:
        //             booking.Status = BookingStatus.CheckedIn;
        //             break;
        //         case BookingStatus.CheckedIn:
        //             booking.Status = BookingStatus.CheckedOut;
        //             break;
        //         case BookingStatus.CheckedOut:
        //             return BadRequest(new { message = "Booking already checked out." });
        //         default:
        //             return BadRequest(new { message = "Unknown booking status." });
        //     }

        //     await _context.SaveChangesAsync();

        //     return Ok(new { message = $"Booking status updated."  });
        // }

        [HttpPost("scan-info")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager},{Roles.Staff}")]
        public async Task<IActionResult> ScanInfo([FromBody] ScanQrCodeDto dto)
        {
            var booking = await _context.Bookings
                .AsNoTracking()
                .Include(b => b.Event).ThenInclude(e => e.Location)
                .Include(b => b.Child)
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.QrCodeData == dto.QrCodeData);

            if (booking == null)
                return NotFound(new { message = "Invalid QR code." });

            var attendee = booking.IsForChild
                ? (booking.Child != null
                    ? $"{booking.Child.FirstName} {booking.Child.LastName}"
                    : (booking.ReservedBookingAttendeeName ?? "Child"))
                : (booking.User != null
                    ? $"{booking.User.FirstName} {booking.User.LastName}"
                    : "User");

            var ev = booking.Event!;
            var resp = new BookingScanInfoResponse
            {
                BookingId = booking.BookingId,
                EventId = ev.EventId,
                AttendeeName = attendee,
                SessionType = ev.SessionType.ToString(),
                Date = ev.StartDate.ToString("yyyy-MM-dd"),
                StartTime = ev.StartTime.ToString(@"hh\:mm"),
                EndTime = ev.EndTime.ToString(@"hh\:mm"),
                SeatNumber = booking.SeatNumber,
                Status = booking.Status.ToString()
            };

            return Ok(resp);
        }

        [HttpPost("check-in")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager},{Roles.Staff}")]
        public async Task<IActionResult> CheckIn([FromBody] BookingIdDto dto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var booking = await _context.Bookings
                .Include(b => b.Event)
                .FirstOrDefaultAsync(b => b.BookingId == dto.BookingId);

            if (booking == null) return NotFound(new { message = "Booking not found." });

            var ev = booking.Event!;
            // Event Managers: only for their own event

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains(Roles.EventManager) && ev.EventManagerId != user.Id)
                return Unauthorized(new { message = "You are not authorised to update bookings for this event." });

            if (ev.Status == EventStatus.Concluded || ev.Status == EventStatus.Cancelled)
                return BadRequest(new { message = "Cannot check in after the event has concluded or been cancelled." });

            if (booking.Status != BookingStatus.Booked)
                return BadRequest(new { message = "Only 'Booked' bookings can be checked in." });

            booking.Status = BookingStatus.CheckedIn;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Checked in.", status = booking.Status.ToString() });
        }

        [HttpPost("check-out")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager},{Roles.Staff}")]
        public async Task<IActionResult> CheckOut([FromBody] BookingIdDto dto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var booking = await _context.Bookings
                .Include(b => b.Event)
                .FirstOrDefaultAsync(b => b.BookingId == dto.BookingId);

            if (booking == null) return NotFound(new { message = "Booking not found." });

            var ev = booking.Event!;
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains(Roles.EventManager) && ev.EventManagerId != user.Id)
                return Unauthorized(new { message = "You are not authorised to update bookings for this event." });

            if (ev.Status == EventStatus.Concluded || ev.Status == EventStatus.Cancelled)
                return BadRequest(new { message = "Cannot check out after the event has concluded or been cancelled." });

            if (booking.Status != BookingStatus.CheckedIn)
                return BadRequest(new { message = "Only 'CheckedIn' bookings can be checked out." });

            booking.Status = BookingStatus.CheckedOut;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Checked out.", status = booking.Status.ToString() });
        }

        // for manuel overide of event status
        [HttpPost("manual-status")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager}")]
        public async Task<IActionResult> ManualStatusUpdate([FromBody] ManualStatusUpdateDto dto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var booking = await _context.Bookings
                .Include(b => b.Event)
                .FirstOrDefaultAsync(b => b.BookingId == dto.BookingId);

            if (booking == null) return NotFound(new { message = "Booking not found." });

            var ev = booking.Event!;
            var userRoles = await _userManager.GetRolesAsync(user);

            if (userRoles.Contains(Roles.EventManager) && ev.EventManagerId != user.Id)
                return Unauthorized(new { message = "You are not authorised to update bookings for this event." });

            if (ev.Status == EventStatus.Concluded || ev.Status == EventStatus.Cancelled)
                return BadRequest(new { message = "Cannot update booking status after the event has concluded or been cancelled." });

            booking.Status = dto.NewStatus;

            // free the seat when cancelling
            if (dto.NewStatus == BookingStatus.Cancelled)
                booking.SeatNumber = null;

            await _context.SaveChangesAsync();
            return Ok(new { message = $"Booking status manually updated to {dto.NewStatus}." });
        }


        [HttpPost("reserve")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager}")]
        public async Task<IActionResult> ReserveSeat([FromBody] ReserveSeatDto dto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            var eventEntity = await _context.Events
                .Include(e => e.Bookings)
                .FirstOrDefaultAsync(e => e.EventId == dto.EventId);

            if (eventEntity == null || eventEntity.Status != EventStatus.Available)
                return NotFound(new { message = "Event not found or not available." });

            // event managers can only make reservations for their event but admins can do so for any event.
            var userRoles = await _userManager.GetRolesAsync(user);
            if (!userRoles.Contains(Roles.Admin) || eventEntity.EventManagerId != user.Id)
                return Forbid();

            if (eventEntity.Bookings.Any(b => b.SeatNumber == dto.SeatNumber && b.Status != BookingStatus.Cancelled))
                return BadRequest(new { message = "Seat already taken." });

            if (dto.SeatNumber < 1 || dto.SeatNumber > eventEntity.TotalSeats)
                return BadRequest(new { message = "Seat number out of range." });

            if (eventEntity.AvailableSeats <= 0)
                return BadRequest(new { message = "No seats available." });

            var booking = new Booking
            {
                EventId = dto.EventId,
                UserId = user.Id,
                SeatNumber = dto.SeatNumber,
                Status = BookingStatus.Booked,
                QrCodeData = Guid.NewGuid().ToString(),
                ReservedBookingAttendeeName = dto.AttendeeName,
                IsForChild = false
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Seat reserved successfully." });
        }
    }
}
