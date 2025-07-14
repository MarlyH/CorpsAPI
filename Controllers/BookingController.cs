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

        public BookingController(AppDbContext context, UserManager<AppUser> userManager, EmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
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

            if (eventEntity == null)
                return NotFound(new { message = "Event not found." });

            if (eventEntity.Bookings.Any(b => b.SeatNumber == dto.SeatNumber))
                return BadRequest(new { message = "That seat is already taken." });

            if(dto.SeatNumber > eventEntity.TotalSeats || dto.SeatNumber < 1)
                return BadRequest(new { message = "Seat out of bounds." });

            if (eventEntity.AvailableSeats <= 0)
                return BadRequest(new { message = "No seats available." });

            int bookingAge;
            if (dto.IsForChild)
            {
                if (eventEntity.Bookings.Any(b => b.ChildId == dto.ChildId && b.Status != BookingStatus.Cancelled))
                    return BadRequest(new { message = "This child already has a booking for this event." });

                var child = await _context.Children.FindAsync(dto.ChildId);
                if (child == null)
                    return BadRequest(new { message = "Booking is for child but child ID not found." });
                
                bookingAge = child.Age;
            }
            else
            {
                if (eventEntity.Bookings.Any(b => b.UserId == user.Id))
                    return BadRequest(new { message = "You already have a booking for this event." });
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
            {
                return BadRequest(new { message = "Provided age for the booking is not within bounds."});
            }

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

            await _emailService.SendEmailAsync(
                user.Email!,
                "Booking Confirmation",
                emailBody
            );

            return Ok(new { message = "Booking created successfully", booking.BookingId });
        }

        [HttpGet("my")]
        [Authorize]
        public async Task<IActionResult> GetMyBookings()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var bookings = await _context.Bookings
                .Include(b => b.Event)
                .Where(b => b.UserId == userId)
                .Select(b => new BookingResponseDto
                {
                    BookingId = b.BookingId,
                    EventId = b.EventId,
                    EventName = b.Event!.Location!.Name,
                    EventDate = b.Event.StartDate,
                    SeatNumber = b.SeatNumber,
                    Status = b.Status,
                    CanBeLeftAlone = b.CanBeLeftAlone,
                    QrCodeData = b.QrCodeData
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
                .FirstOrDefaultAsync(b => b.BookingId == id);

            if (booking == null || booking.UserId != userId)
                return NotFound(new { message = "Booking not found." });

            // get available seats before we actually cancel booking.
            // This way we can check if the event was full.
            var ev = booking.Event!;
            bool wasFull = ev.AvailableSeats <= 0;

            booking.Status = BookingStatus.Cancelled;
            booking.SeatNumber = null;
            var result = await _context.SaveChangesAsync();

            if (wasFull)
            {
                // send email to anybody on waitlist

                var waitlistEntries = await _context.Waitlists
                    .Where(w => w.EventId == ev.EventId)
                    .Include(w => w.User)
                    .ToListAsync();

                foreach (var entry in waitlistEntries)
                {
                    var user = entry.User!;
                    var emailBody = $@"
                            <p>Dear {user.UserName},</p>
                            <p>A seat has been made available for the event on <strong>{ev.StartDate}</strong> at <strong>{ev.StartTime}</strong>.</p>";

                    await emailService.SendEmailAsync(
                        user.Email!,
                        "Seat Available: Event Notification",
                        emailBody
                    );

                    // now mark waitlist entity for deletion
                    _context.Remove(entry);
                    
                }
                // delete all at once
                await _context.SaveChangesAsync();
            }
            return Ok(new { message = "Booking successfully cancelled." });
        }

        [HttpPost("scan")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager},{Roles.Staff}")]
        public async Task<IActionResult> ScanQrCode([FromBody] ScanQrCodeDto dto)
        {
            var booking = await _context.Bookings
                .Include(b => b.Event)
                .FirstOrDefaultAsync(b => b.QrCodeData == dto.QrCodeData);

            if (booking == null)
                return NotFound(new { message = "Invalid QR code." });

            switch (booking.Status)
            {
                case BookingStatus.Booked:
                    booking.Status = BookingStatus.CheckedIn;
                    break;
                case BookingStatus.CheckedIn:
                    booking.Status = BookingStatus.CheckedOut;
                    break;
                case BookingStatus.CheckedOut:
                    return BadRequest(new { message = "Booking already checked out." });
                default:
                    return BadRequest(new { message = "Unknown booking status." });
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = $"Booking status updated."  });
        }
    }     
}
