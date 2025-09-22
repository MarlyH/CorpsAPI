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
using CorpsAPI.DTOs.Child;
using QRCoder;
using System.Net.Mime;
using System.Linq;


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


        private static DateTime? GetSuspendedUntil(AppUser user)
        {
            // If there’s no recorded strike date, we can’t compute an until date.
            if (user.DateOfLastStrike is null) return null;

            // 90-day suspension from last strike date (stored as DateOnly).
            var until = user.DateOfLastStrike.Value.ToDateTime(TimeOnly.MinValue).AddDays(90);

            // If already expired, return null so the caller knows it’s no longer active.
            return until.Date > DateTime.Today ? until.Date : (DateTime?)null;
        }

        [HttpPost]
        [Authorize(Roles = $"{Roles.User},{Roles.Admin},{Roles.EventManager},{Roles.Staff}")]
        public async Task<IActionResult> CreateBooking([FromBody] CreateBookingDto dto)
        {
            
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Trigger the computed property (it may auto-clear if 90+ days elapsed).
            var suspended = user.IsSuspended;

            if (suspended || user.AttendanceStrikeCount >= 3)
            {
                var until = GetSuspendedUntil(user);
                // Return a JSON payload with the ban-until date.
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = until is not null
                        ? $"Your account is suspended until {until:yyyy-MM-dd}. You cannot book events."
                        : "Your account is suspended. You cannot book events.",
                    suspensionUntil = until?.ToString("yyyy-MM-dd"),
                    strikes = user.AttendanceStrikeCount
                });
            }


            var eventEntity = await _context.Events
                .Include(e => e.Bookings)
                .Include(e => e.Location)
                .FirstOrDefaultAsync(e => e.EventId == dto.EventId);

            if (eventEntity == null || eventEntity.Status != EventStatus.Available)
                return NotFound(new { message = "Event not found or not available for booking." });

            // Seat number bounds
            if (dto.SeatNumber < 1 || dto.SeatNumber > eventEntity.TotalSeats)
                return BadRequest(new { message = "Seat out of bounds." });

            // Seat taken?
            if (eventEntity.Bookings.Any(b =>
                    b.SeatNumber == dto.SeatNumber &&
                    b.Status != BookingStatus.Cancelled &&
                    b.Status != BookingStatus.Striked))
            {
                return BadRequest(new { message = "That seat is already taken." });
            }

            // Capacity (count only active bookings that actually hold a seat)
            var occupiedSeats = eventEntity.Bookings.Count(b =>
                b.SeatNumber != null &&
                b.Status != BookingStatus.Cancelled &&
                b.Status != BookingStatus.Striked);

            if (occupiedSeats >= eventEntity.TotalSeats)
                return BadRequest(new { message = "No seats available." });

            // Age/duplicate checks
            int bookingAge;
            if (dto.IsForChild)
            {
                // ensure child exists
                var child = await _context.Children.FindAsync(dto.ChildId);
                if (child == null)
                    return BadRequest(new { message = "Booking is for child but child ID not found." });

                // duplicate for child (ignore cancelled/striked)
                if (eventEntity.Bookings.Any(b =>
                        b.ChildId == dto.ChildId &&
                        b.Status != BookingStatus.Cancelled &&
                        b.Status != BookingStatus.Striked))
                {
                    return BadRequest(new { message = "This child already has a booking for this event." });
                }

                bookingAge = child.Age;
            }
            else
            {
                // duplicate for user (ignore cancelled/striked)
                if (eventEntity.Bookings.Any(b =>
                        b.UserId == user.Id &&
                        b.Status != BookingStatus.Cancelled &&
                        b.Status != BookingStatus.Striked))
                {
                    return BadRequest(new { message = "You already have an active booking for this event." });
                }

                bookingAge = user.Age; // <-- fix
            }

            // Age gate
            bool ageIsOk = false;
            switch (eventEntity.SessionType)
            {
                case EventSessionType.Kids:
                    ageIsOk = (bookingAge >= 5 && bookingAge < 12);
                    break;
                case EventSessionType.Teens:
                    ageIsOk = (bookingAge >= 12 && bookingAge < 16);
                    break;
                case EventSessionType.Adults:
                    ageIsOk = (bookingAge >= 16);
                    break;
                default:
                    return BadRequest(new { message = ErrorMessages.InternalServerError });
            }

            if (!ageIsOk)
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
            var qrGen = new QRCodeGenerator();
            var qrData = qrGen.CreateQrCode(booking.QrCodeData, QRCodeGenerator.ECCLevel.Q);
            var qrPng = new PngByteQRCode(qrData);
            byte[] qrBytes = qrPng.GetGraphic(10); // pixels per module

            const string qrCid = "booking_qr";

            var emailBody = $@"
            <p>Dear {user.UserName},</p>
            <p>Thank you for your booking. Your reservation has been confirmed:</p>
            <ul>
                <li><strong>Date:</strong> {eventEntity.StartDate:dddd, MMMM d, yyyy}</li>
                <li><strong>Time:</strong> {eventEntity.StartTime:h:mm} - {eventEntity.EndTime:h:mm}</li>
                <li><strong>Location:</strong> {eventEntity.Location?.Name ?? "TBA"}, {eventEntity.Address ?? "No address provided"}</li>
                <li><strong>Session Type:</strong> {eventEntity.SessionType}</li>
            </ul>
            <p>Present this QR code at the entrance to check in/out:</p>
            <p style=""text-align:center"">
                <img src=""cid:{qrCid}"" alt=""Your Corps ticket QR"" width=""200"" height=""200"" />
            </p>
            <p>You can also view this ticket anytime in the app.</p>
            <p>Warm regards,<br/>The Your Corps Team</p>
            ";

            // Fire-and-forget to avoid slowing the response
            _ = Task.Run(async () =>
            {
                try
                {
                    await _emailService.SendEmailWithInlineAsync(
                        user.Email!, "Booking Confirmation", emailBody, qrBytes, qrCid, MediaTypeNames.Image.Png);
                }
                catch
                {
                    // TODO: log
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
                    IsForChild = b.IsForChild,
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
                b.Status != BookingStatus.Cancelled &&
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

            var ev = booking.Event!;
            var attendee = booking.IsForChild
                ? (booking.Child != null
                    ? $"{booking.Child.FirstName} {booking.Child.LastName}"
                    : (booking.ReservedBookingAttendeeName ?? "Child"))
                : (booking.User != null
                    ? $"{booking.User.FirstName} {booking.User.LastName}"
                    : "User");

            // CHILD block (medical included if present)
            ChildDto? childDto = null;
            if (booking.IsForChild && booking.Child != null)
            {
                // Load child medical conditions
                var childMeds = await _context.ChildMedicalConditions
                    .AsNoTracking()
                    .Where(m => m.ChildId == booking.Child.ChildId)
                    .OrderByDescending(m => m.IsAllergy) // allergies first (optional)
                    .ThenBy(m => m.Name)
                    .Select(m => new MedicalConditionDto
                    {
                        Id = m.Id,
                        Name = m.Name,
                        Notes = m.Notes,
                        IsAllergy = m.IsAllergy
                    })
                    .ToListAsync();

                childDto = new ChildDto
                {
                    ChildId = booking.Child.ChildId,
                    FirstName = booking.Child.FirstName,
                    LastName = booking.Child.LastName,
                    DateOfBirth = booking.Child.DateOfBirth,
                    EmergencyContactName = booking.Child.EmergencyContactName,
                    EmergencyContactPhone = booking.Child.EmergencyContactPhone,
                    Age = CalculateAge(booking.Child.DateOfBirth),

                    // If your ChildDto has these fields now:
                    HasMedicalConditions = childMeds.Count > 0,
                    MedicalConditions = childMeds
                };
            }

            // USER mini block (medical included if present)
            AdminUserMiniDto? userMini = null;
            if (booking.User != null)
            {
                // trigger IsSuspended property logic (your side-effect)
                _ = booking.User.IsSuspended;

                var userMeds = await _context.UserMedicalConditions
                    .AsNoTracking()
                    .Where(m => m.UserId == booking.User.Id)
                    .OrderByDescending(m => m.IsAllergy)
                    .ThenBy(m => m.Name)
                    .Select(m => new MedicalConditionDto
                    {
                        Id = m.Id,
                        Name = m.Name,
                        Notes = m.Notes,
                        IsAllergy = m.IsAllergy
                    })
                    .ToListAsync();

                userMini = new AdminUserMiniDto
                {
                    Id = booking.User.Id,
                    Email = booking.User.Email,
                    PhoneNumber = booking.User.PhoneNumber,
                    FirstName = booking.User.FirstName,
                    LastName = booking.User.LastName,

                    // Safer to compute rather than read a non-existent property:
                    HasMedicalConditions = userMeds.Count > 0,
                    MedicalConditions = userMeds,

                    AttendanceStrikeCount = booking.User.AttendanceStrikeCount,
                    DateOfLastStrike = booking.User.DateOfLastStrike,
                    IsSuspended = booking.User.IsSuspended
                };
            }

            var resp = new BookingScanDetailDto
            {
                // Booking + Event
                BookingId = booking.BookingId,
                EventId = ev.EventId,
                EventName = ev.Location?.Name,
                EventDate = ev.StartDate,
                StartTime = ev.StartTime.ToString(@"hh\:mm"),
                EndTime = ev.EndTime.ToString(@"hh\:mm"),
                SessionType = ev.SessionType.ToString(),
                LocationName = ev.Location?.Name,
                Address = ev.Address,

                // Booking fields
                SeatNumber = booking.SeatNumber,
                Status = booking.Status,
                CanBeLeftAlone = booking.CanBeLeftAlone,
                QrCodeData = booking.QrCodeData,
                IsForChild = booking.IsForChild,
                AttendeeName = attendee,

                // Related
                Child = childDto,
                User = userMini
            };

            return Ok(resp);
        }


        private static int CalculateAge(DateOnly dob)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var age = today.Year - dob.Year;
            if (today.Month < dob.Month || (today.Month == dob.Month && today.Day < dob.Day))
                age--;
            return age;
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
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.BookingId == dto.BookingId);

            if (booking == null) return NotFound(new { message = "Booking not found." });

            var ev = booking.Event!;
            var userRoles = await _userManager.GetRolesAsync(user);

            if (userRoles.Contains(Roles.EventManager) && ev.EventManagerId != user.Id)
                return Unauthorized(new { message = "You are not authorised to update bookings for this event." });

            if (ev.Status == EventStatus.Concluded || ev.Status == EventStatus.Cancelled)
                return BadRequest(new { message = "Cannot update booking status after the event has concluded or been cancelled." });

            booking.Status = dto.NewStatus;

            // Cancel frees seat
            if (dto.NewStatus == BookingStatus.Cancelled)
                booking.SeatNumber = null;

            // Striked logic
            if (dto.NewStatus == BookingStatus.Striked)
            {
                booking.SeatNumber = null;

                if (booking.User != null)
                {
                    booking.User.AttendanceStrikeCount += 1;
                    booking.User.DateOfLastStrike = DateOnly.FromDateTime(DateTime.Today);
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = $"Booking status manually updated to {dto.NewStatus}." });
        }

        [HttpPost("reserve")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager}")]
        public async Task<IActionResult> ReserveSeat([FromBody] ReserveSeatDto dto)
        {
            if (dto is null) return BadRequest(new { message = "Invalid payload." });

            // Required fields
            if (dto.SeatNumber <= 0)
                return BadRequest(new { message = "SeatNumber must be >= 1." });
            if (string.IsNullOrWhiteSpace(dto.AttendeeName))
                return BadRequest(new { message = "AttendeeName is required." });
            if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
                return BadRequest(new { message = "PhoneNumber is required." });

            // Normalize/validate phone (simple sanitize)
            var cleanedPhone = new string(dto.PhoneNumber.Trim()
                .Where(c => char.IsDigit(c) || c == '+')
                .ToArray());
            if (cleanedPhone.Length < 7)
                return BadRequest(new { message = "Invalid phone number." });

            // NEW: if cannot be left alone, require Parent/Guardian name
            var guardianName = dto.ReservedBookingParentGuardianName?.Trim();
            if (!dto.CanBeLeftAlone && string.IsNullOrWhiteSpace(guardianName))
                return BadRequest(new { message = "Parent/Guardian name is required when attendee cannot be left alone." });

            // Current user
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Event + current bookings
            var eventEntity = await _context.Events
                .Include(e => e.Bookings)
                .FirstOrDefaultAsync(e => e.EventId == dto.EventId);

            if (eventEntity == null || eventEntity.Status != EventStatus.Available)
                return NotFound(new { message = "Event not found or not available." });

            // Optional: seat bounds
            if (dto.SeatNumber > eventEntity.TotalSeats)
                return BadRequest(new { message = "Seat out of bounds." });

            // Allow Admin OR the EventManager of this event
            var userRoles = await _userManager.GetRolesAsync(user);
            if (!userRoles.Contains(Roles.Admin) && eventEntity.EventManagerId != user.Id)
                return Forbid();

            // Seat already taken? (ignore cancelled/striked)
            if (eventEntity.Bookings.Any(b =>
                b.SeatNumber == dto.SeatNumber &&
                b.Status != BookingStatus.Cancelled &&
                b.Status != BookingStatus.Striked))
            {
                return BadRequest(new { message = "Seat already taken." });
            }

            // Capacity check (only active bookings with a seat)
            var occupiedSeats = eventEntity.Bookings.Count(b =>
                b.SeatNumber != null &&
                b.Status != BookingStatus.Cancelled &&
                b.Status != BookingStatus.Striked);

            if (occupiedSeats >= eventEntity.TotalSeats)
                return BadRequest(new { message = "No seats available." });

            // Create booking
            var booking = new Booking
            {
                EventId = dto.EventId,
                UserId = user.Id,
                SeatNumber = dto.SeatNumber,
                Status = BookingStatus.Booked,
                QrCodeData = Guid.NewGuid().ToString(),
                IsForChild = false,

                // Reservation fields
                ReservedBookingAttendeeName = dto.AttendeeName?.Trim(),
                ReservedBookingPhone = cleanedPhone,
                CanBeLeftAlone = dto.CanBeLeftAlone,
                ReservedBookingParentGuardianName = dto.CanBeLeftAlone ? null : guardianName
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Seat reserved successfully.", bookingId = booking.BookingId });
        }

    }
}
