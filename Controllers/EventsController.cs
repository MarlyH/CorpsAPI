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
            var events = await _context.Events
                .Where(e => !e.IsCancelled)
                .Include(e => e.Location)
                .Include(e => e.Bookings)
                .ToListAsync();

            var dtos = events.Select(e => new GetAllEventsDto(e)).ToList();

            return Ok(dtos);
        }

        // GET: api/Events/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEvent(int id)
        {
            var ev = await _context.Events
                .Include(e => e.Location)
                .Include(e => e.Bookings)
                .FirstOrDefaultAsync(e => e.EventId == id && !e.IsCancelled);

            if (ev == null)
                return NotFound(new { message = ErrorMessages.EventNotFound });

            var dto = new GetEventDto(ev);

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

            // upload the seating map image
            string imageUrl;
            try
            {
                imageUrl = await _azureStorageService.UploadImageAsync(dto.SeatingMapImage);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Image upload failed", detail = ex.Message });
            }

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
                Address = dto.Address
            };

            _context.Events.Add(newEvent);
            await _context.SaveChangesAsync();

            return Ok(new { message = SuccessMessages.EventCreateSuccessful });
        }

        [HttpPut("{id}/cancel")]
        [Authorize(Roles = $"{Roles.EventManager}, {Roles.Admin}")]
        public async Task<IActionResult> CancelEvent(int id, [FromBody] CancelEventRequestDto dto, [FromServices] EmailService emailService)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = ErrorMessages.InvalidRequest });

            var ev = await _context.Events
                .Include(e => e.Bookings)
                    .ThenInclude(b => b.User)
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (ev == null)
                return NotFound(new { message = ErrorMessages.InvalidRequest });

            // Only the manager for the particular event or an admin should be able to cancel
            if (ev.EventManagerId != userId && !User.IsInRole(Roles.Admin))
                return BadRequest(new { message = ErrorMessages.EventCancelUnauthorised});

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

            ev.IsCancelled = true;
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

            var alreadyExists = await _context.Waitlists
                .AnyAsync(w => w.EventId == eventId && w.UserId == userId);
            if (alreadyExists)
                return BadRequest(new { message = ErrorMessages.AlreadyOnWaitlist });

            var ev = await _context.Events.FindAsync(eventId);
            if (ev == null)
                return NotFound(new { message = ErrorMessages.EventNotFound });

            if (ev.AvailableSeats > 0)
                return BadRequest(new { message = ErrorMessages.SeatsStillAvailable });

            var entry = new Waitlist
            {
                EventId = eventId,
                UserId = userId
            };

            _context.Waitlists.Add(entry);
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
    }
}
