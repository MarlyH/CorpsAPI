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

        public BookingController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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
                .FirstOrDefaultAsync(e => e.EventId == dto.EventId);

            if (eventEntity == null)
                return NotFound(new { message = "Event not found." });

            if (eventEntity.Bookings.Any(b => b.SeatNumber == dto.SeatNumber))
                return BadRequest(new { message = "That seat is already taken." });

            if (eventEntity.AvailableSeats <= 0)
                return BadRequest(new { message = "No seats available." });

            var booking = new Booking
            {
                EventId = dto.EventId,
                SeatNumber = dto.SeatNumber,
                CanBeLeftAlone = dto.CanBeLeftAlone,
                AttendingUserId = user.Id,
                Status = BookingStatus.Booked,
                QrCodeData = Guid.NewGuid().ToString()
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

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
                .Where(b => b.AttendingUserId == userId)
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

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> CancelBooking(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var booking = await _context.Bookings.FindAsync(id);

            if (booking == null || booking.AttendingUserId != userId)
                return NotFound(new { message = "Booking not found." });

            _context.Bookings.Remove(booking);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Booking cancelled." });
        }
    }
}
