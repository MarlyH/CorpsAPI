using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using CorpsAPI.Constants;
using CorpsAPI.Data;
using CorpsAPI.DTOs;
using CorpsAPI.Models;
using CorpsAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CorpsAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EventsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly AzureStorageService _azureStorageService;

        public EventsController(AppDbContext context, AzureStorageService azureStorageService)
        {
            _context = context;
            _azureStorageService = azureStorageService;
        }

        // GET: api/Events
        [HttpGet]
        public async Task<IActionResult> GetEvents()
        {
            var list = await _context.Events
                .Where(e => e.Status == EventStatus.Available)
                .Select(e => new
                {
                    e.EventId,
                    LocationName = e.Location!.Name,
                    e.StartDate,
                    e.StartTime,
                    e.EndTime,
                    e.SessionType,
                    e.SeatingMapImgSrc,
                    e.TotalSeats,
                    AvailableSeatsCount = e.TotalSeats - _context.Bookings.Count(b =>
                        b.EventId == e.EventId &&
                        b.Status  != BookingStatus.Cancelled &&
                        b.SeatNumber != null)
                })
                .ToListAsync();

            return Ok(list);
        }


        // GET: api/Events/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEvent(int id)
        {
            var ev = await _context.Events
                .Include(e => e.Location)
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (ev == null)
                return NotFound(new { message = ErrorMessages.EventNotFound });

            // Seats currently taken by active bookings
            var taken = await _context.Bookings
                .Where(b => b.EventId == id &&
                            b.Status != BookingStatus.Cancelled &&
                            b.SeatNumber != null)
                .Select(b => b.SeatNumber!.Value)
                .ToListAsync();

            var available = Enumerable.Range(1, ev.TotalSeats).Except(taken).ToList();

            // (Optional but recommended) prevent any caching
            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";

            // Shape the payload to what the app expects
            var dto = new
            {
                eventId = ev.EventId,
                locationName = ev.Location?.Name,
                startDate = ev.StartDate,
                startTime = ev.StartTime,
                endTime = ev.EndTime,
                sessionType = ev.SessionType,
                seatingMapImgSrc = ev.SeatingMapImgSrc,
                description = ev.Description,
                address = ev.Address,

                totalSeats = ev.TotalSeats,
                availableSeats = available,
                availableSeatsCount = available.Count,

                status = ev.Status
            };

            return Ok(dto);
        }


        // POST: api/Events
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        [Authorize(Roles = $"{Roles.EventManager}, {Roles.Admin}")]
        [RequestSizeLimit(10 * 1024 * 1024)] // Limit to 10MB
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> PostEvent([FromForm] CreateEventDto dto)
        {
            var eventManagerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(eventManagerId))
                return Unauthorized(new { message = ErrorMessages.InvalidRequest });

            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(e => e.Value?.Errors.Count > 0)
                    .SelectMany(e => e.Value!.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToArray();

                return BadRequest(new { message = errors });
            }

            if (dto.AvailableDate > dto.StartDate)
                return BadRequest(new { message = ErrorMessages.EventNotAvailable });

            string? imageUrl = null;
            if (dto.SeatingMapImage != null)
            {
                // upload the seating map image
                try
                {
                    imageUrl = await _azureStorageService.UploadImageAsync(dto.SeatingMapImage);
                }
                catch (Exception ex)
                {
                    return BadRequest(new { message = "Image upload failed", detail = ex.Message });
                }
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var eventStatus = dto.AvailableDate <= today ? EventStatus.Available : EventStatus.Unavailable;

            var newEvent = new Event
            {
                LocationId = dto.LocationId,
                EventManagerId = eventManagerId,
                SessionType = dto.SessionType,
                StartDate = dto.StartDate,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                AvailableDate = dto.AvailableDate,
                SeatingMapImgSrc = imageUrl,
                TotalSeats = dto.TotalSeats,
                Description = dto.Description,
                Address = dto.Address,
                Status = eventStatus
            };

            _context.Events.Add(newEvent);
            await _context.SaveChangesAsync();

            return Ok(new { message = SuccessMessages.EventCreateSuccessful });
        }

        [HttpPut("{id}/cancel")]
        [Authorize(Roles = $"{Roles.EventManager}, {Roles.Admin}")]
        public async Task<IActionResult> CancelEvent(int id, [FromBody] CancelEventRequestDto dto, [FromServices] EmailService emailService)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = ErrorMessages.InvalidRequest });

            var ev = await _context.Events
                .Include(e => e.Bookings)
                    .ThenInclude(b => b.User)
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (ev == null)
                return NotFound(new { message = ErrorMessages.InvalidRequest });

            // Only the manager for the particular event or an admin should be able to cancel
            if (ev.EventManagerId != userId || !User.IsInRole(Roles.Admin))
                return BadRequest(new { message = ErrorMessages.EventCancelUnauthorised });

            // Cancel all associated bookings that aren't already cancelled
            foreach (var booking in ev.Bookings.Where(b => b.Status != BookingStatus.Cancelled))
            {
                booking.Status = BookingStatus.Cancelled;

                if (!string.IsNullOrWhiteSpace(booking.User?.Email))
                {
                    var emailBody = $@"
                <p>Dear {booking.User.UserName},</p>
                <p>The event you booked on <strong>{ev.StartDate}</strong> at <strong>{ev.StartTime}</strong> has been <strong>cancelled</strong>.</p>";

                    if (!string.IsNullOrWhiteSpace(dto.CancellationMessage))
                        emailBody += $"<p><strong>Message from the organiser:</strong> {dto.CancellationMessage}</p>";

                    emailBody += "<p>We apologise for any inconvenience caused.</p>";

                    await emailService.SendEmailAsync(
                        booking.User.Email,
                        "Your event has been cancelled",
                        emailBody
                    );
                }
            }

            ev.Status = EventStatus.Cancelled;
            await _context.SaveChangesAsync();

            return Ok(new { message = SuccessMessages.EventCancelSuccessful });
        }

        // Waitlist stuff

        [HttpGet("{eventId}/waitlist")]
        [Authorize(Roles = $"{Roles.EventManager}, {Roles.Admin}")]
        public async Task<IActionResult> GetWaitlist(int eventId)
        {
            var waitlist = await _context.Waitlists
                .Where(w => w.EventId == eventId)
                .Select(w => new GetWaitlistDto(w))
                .ToListAsync();

            return Ok(waitlist);
        }

        [HttpPost("{eventId}/waitlist")]
        [Authorize]
        public async Task<IActionResult> AddToWaitlist(int eventId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = ErrorMessages.InvalidRequest });

            var already = await _context.Waitlists.AnyAsync(w => w.EventId == eventId && w.UserId == userId);
            if (already) return BadRequest(new { message = ErrorMessages.AlreadyOnWaitlist });

            var ev = await _context.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
            if (ev == null) return NotFound(new { message = ErrorMessages.EventNotFound });

            var activeCount = await _context.Bookings.CountAsync(b =>
                b.EventId == eventId &&
                b.Status  != BookingStatus.Cancelled &&
                b.SeatNumber != null);

            if (activeCount < ev.TotalSeats)
                return BadRequest(new { message = ErrorMessages.SeatsStillAvailable });

            _context.Waitlists.Add(new Waitlist { EventId = eventId, UserId = userId });
            await _context.SaveChangesAsync();

            return Ok(new { message = SuccessMessages.WaitlistAddSuccessful });
        }


        [HttpDelete("{eventId}/waitlist")]
        [Authorize]
        public async Task<IActionResult> RemoveFromWaitlist(int eventId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = ErrorMessages.InvalidRequest });

            var entry = await _context.Waitlists
                .FirstOrDefaultAsync(w => w.EventId == eventId && w.UserId == userId);

            if (entry == null)
                return NotFound(new { message = ErrorMessages.NotOnWaitlist });

            _context.Waitlists.Remove(entry);
            await _context.SaveChangesAsync();

            return Ok(new { message = SuccessMessages.WaitlistRemoveSuccessful });
        }


        // GET: api/Events/{eventId}/attendees
        [HttpGet("{eventId}/attendees")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager}")]
        public async Task<IActionResult> GetAttendeesForEvent(int eventId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            // EventManager can only access their own events (Admin can see all)
            if (User.IsInRole(Roles.EventManager) && !User.IsInRole(Roles.Admin))
            {
                var ownerId = await _context.Events
                    .Where(e => e.EventId == eventId)
                    .Select(e => e.EventManagerId)
                    .FirstOrDefaultAsync();

                if (ownerId == null) return NotFound(new { message = ErrorMessages.EventNotFound });
                if (ownerId != userId) return Forbid();
            }

            var bookings = await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Child)
                .Where(b => b.EventId == eventId)
                .ToListAsync();

            var result = bookings.Select(b => new EventAttendeeDto
            {
                BookingId = b.BookingId,
                Name = b.IsForChild
                    ? (b.Child != null
                        ? $"{b.Child.FirstName} {b.Child.LastName}"
                        : (b.ReservedBookingAttendeeName ?? "Child"))
                    : (b.ReservedBookingAttendeeName
                        ?? (b.User != null ? $"{b.User.FirstName} {b.User.LastName}" : "User")),
                Status     = b.Status,
                SeatNumber = b.SeatNumber,
                IsForChild = b.IsForChild
            });

            return Ok(result);
        }

        // POST: api/Events/{eventId}/attendance
        [HttpPost("{eventId}/attendance")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager}")]
        public async Task<IActionResult> UpdateAttendance(int eventId, [FromBody] UpdateAttendanceDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            if (User.IsInRole(Roles.EventManager) && !User.IsInRole(Roles.Admin))
            {
                var ownerId = await _context.Events.Where(e => e.EventId == eventId)
                    .Select(e => e.EventManagerId).FirstOrDefaultAsync();
                if (ownerId == null) return NotFound(new { message = ErrorMessages.EventNotFound });
                if (ownerId != userId) return Forbid();
            }

            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.EventId == eventId && b.BookingId == dto.BookingId);
            if (booking == null) return NotFound(new { message = "Booking not found." });

            booking.Status = dto.NewStatus;
            if (dto.NewStatus == BookingStatus.Cancelled)
                booking.SeatNumber = null; // free it

            await _context.SaveChangesAsync();
            return Ok(new { message = $"Attendance updated to {dto.NewStatus}." });
        }


    }
    
}
