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
using System.Globalization;
using System.Text.Json;

namespace CorpsAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EventsController : ControllerBase
    {
        private const long MaxEventImageBytes = 4 * 1024 * 1024;
        private readonly AppDbContext _context;
        private readonly AzureStorageService _azureStorageService;

        public EventsController(AppDbContext context, AzureStorageService azureStorageService)
        {
            _context = context;
            _azureStorageService = azureStorageService;
        }
        private static string FormatTime12(TimeOnly t)
            => t.ToString("h:mm tt", CultureInfo.InvariantCulture).ToLowerInvariant();

        // Frontend expects: 0=available, 1=unavailable, 2=cancelled, 3=concluded
        private static int ToClientEventStatus(EventStatus status) => status switch
        {
            EventStatus.Available => 0,
            EventStatus.Unavailable => 1,
            EventStatus.Cancelled => 2,
            EventStatus.Concluded => 3,
            _ => 1
        };

        private static bool TryParseCategory(string? raw, out EventCategory category)
        {
            category = EventCategory.Bookable;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            var normalized = raw.Trim().ToLowerInvariant()
                .Replace(" ", "")
                .Replace("_", "")
                .Replace("-", "");

            if (normalized.Contains("promo"))
            {
                category = EventCategory.Promotional;
                return true;
            }

            if (normalized.Contains("announce") || normalized.Contains("content") || normalized.Contains("custom"))
            {
                category = EventCategory.Announcement;
                return true;
            }

            if (normalized.Contains("book"))
            {
                category = EventCategory.Bookable;
                return true;
            }

            return Enum.TryParse(raw, true, out category);
        }

        private static List<string> ParseEventImageUrls(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new List<string>();

            var trimmed = raw.Trim();
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<List<string>>(trimmed);
                    if (parsed != null)
                    {
                        return parsed
                            .Where(url => !string.IsNullOrWhiteSpace(url))
                            .Select(url => url.Trim())
                            .ToList();
                    }
                }
                catch
                {
                    // Backward compatibility: treat invalid JSON as a legacy single URL string.
                }
            }

            return new List<string> { trimmed };
        }

        private static string? SerializeEventImageUrls(IEnumerable<string> urls)
        {
            var normalized = urls
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Select(url => url.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalized.Count == 0)
                return null;
            if (normalized.Count == 1)
                return normalized[0];

            return JsonSerializer.Serialize(normalized);
        }

        private async Task DeleteEventImagesAsync(string? eventImageImgSrcRaw)
        {
            var urls = ParseEventImageUrls(eventImageImgSrcRaw);
            foreach (var imageUrl in urls)
            {
                await _azureStorageService.DeleteImageAsync(imageUrl);
            }
        }

        // GET: api/Events
        [HttpGet]
        public async Task<IActionResult> GetEvents()
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var rawList = await _context.Events
                .Where(e =>
                    e.Status == EventStatus.Available ||
                    (!e.RequiresBooking &&
                     e.Status != EventStatus.Cancelled &&
                     e.Status != EventStatus.Concluded &&
                     (!e.EndDate.HasValue || e.EndDate >= today)))
                .Select(e => new
                {
                    eventId = e.EventId,
                    locationId = e.LocationId,
                    locationName = e.Location != null ? e.Location.Name : (e.Title ?? "Event"),
                    title = e.Title,
                    eventCategory = e.Category,
                    category = e.Category,
                    requiresBooking = e.RequiresBooking,
                    startDate = e.StartDate,
                    fromDate = e.StartDate,
                    endDate = e.EndDate,
                    toDate = e.EndDate,
                    startTime = e.StartTime,
                    endTime = e.EndTime,
                    sessionType = e.SessionType,
                    description = e.Description,
                    address = e.Address,
                    seatingMapImgSrc = e.SeatingMapImgSrc,
                    eventImageImgSrcRaw = e.EventImageImgSrc,
                    totalSeats = e.RequiresBooking ? e.TotalSeats : 0,
                    totalSeatsCount = e.RequiresBooking ? e.TotalSeats : 0,
                    createdByEmail = e.EventManager != null ? e.EventManager.Email : null,
                    availableSeatsCount = e.RequiresBooking
                        ? e.TotalSeats - _context.Bookings.Count(b =>
                            b.EventId == e.EventId &&
                            b.Status != BookingStatus.Cancelled &&
                            b.SeatNumber != null)
                        : 0,
                    status = e.Status == EventStatus.Available ? 0
                        : e.Status == EventStatus.Unavailable ? 1
                        : e.Status == EventStatus.Cancelled ? 2
                        : 3,
                    availbleSeatsCount = e.RequiresBooking
                        ? e.TotalSeats - _context.Bookings.Count(b =>
                            b.EventId == e.EventId &&
                            b.Status != BookingStatus.Cancelled &&
                            b.SeatNumber != null)
                        : 0
                })
                .ToListAsync();

            var list = rawList.Select(e =>
            {
                var imageUrls = ParseEventImageUrls(e.eventImageImgSrcRaw);
                return new
                {
                    e.eventId,
                    e.locationId,
                    e.locationName,
                    e.title,
                    e.eventCategory,
                    e.category,
                    e.requiresBooking,
                    e.startDate,
                    e.fromDate,
                    e.endDate,
                    e.toDate,
                    e.startTime,
                    e.endTime,
                    e.sessionType,
                    e.description,
                    e.address,
                    e.seatingMapImgSrc,
                    eventImageImgSrc = imageUrls.FirstOrDefault(),
                    eventImageImgSrcs = imageUrls,
                    e.totalSeats,
                    e.totalSeatsCount,
                    e.createdByEmail,
                    e.availableSeatsCount,
                    e.status,
                    e.availbleSeatsCount
                };
            });

            return Ok(list);
        }


        // GET: api/Events/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEvent(int id)
        {
            var ev = await _context.Events
                .Include(e => e.Location)
                .Include(e => e.EventManager)
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (ev == null)
                return NotFound(new { message = ErrorMessages.EventNotFound });

            var available = new List<int>();
            if (ev.RequiresBooking && ev.TotalSeats > 0)
            {
                // Seats currently taken by active bookings
                var taken = await _context.Bookings
                    .Where(b => b.EventId == id &&
                                b.Status != BookingStatus.Cancelled &&
                                b.SeatNumber != null)
                    .Select(b => b.SeatNumber!.Value)
                    .ToListAsync();

                available = Enumerable.Range(1, ev.TotalSeats).Except(taken).ToList();
            }

            // (Optional but recommended) prevent any caching
            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            var imageUrls = ParseEventImageUrls(ev.EventImageImgSrc);

            // Shape the payload to what the app expects
            var dto = new
            {
                eventId = ev.EventId,
                locationId = ev.LocationId,
                locationName = ev.Location?.Name ?? ev.Title ?? "Event",
                locationMascotImgSrc = ev.Location?.MascotImgSrc,
                title = ev.Title,
                eventCategory = ev.Category,
                category = ev.Category,
                requiresBooking = ev.RequiresBooking,
                startDate = ev.StartDate,
                fromDate = ev.StartDate,
                endDate = ev.EndDate,
                toDate = ev.EndDate,
                startTime = ev.StartTime,
                endTime = ev.EndTime,
                sessionType = ev.SessionType,
                seatingMapImgSrc = ev.SeatingMapImgSrc,
                eventImageImgSrc = imageUrls.FirstOrDefault(),
                eventImageImgSrcs = imageUrls,
                description = ev.Description,
                address = ev.Address,
                createdByEmail = ev.EventManager?.Email,
                totalSeats = ev.RequiresBooking ? ev.TotalSeats : 0,
                totalSeatsCount = ev.RequiresBooking ? ev.TotalSeats : 0,
                availableSeats = available,
                availableSeatsCount = ev.RequiresBooking ? available.Count : 0,
                status = ToClientEventStatus(ev.Status),
                availbleSeatsCount = ev.RequiresBooking ? available.Count : 0
            };

            return Ok(dto);
        }

        // GET: api/Events/manage
        [HttpGet("manage")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager}")]
        public async Task<IActionResult> GetManageableEvents()
        {
            var userId  = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var isAdmin = User.IsInRole(Roles.Admin);

            IQueryable<Event> q = _context.Events
                .AsNoTracking()
                .Where(e => e.Status == EventStatus.Available && e.RequiresBooking);

            // Admin sees ALL; EventManager sees ONLY their own
            if (!isAdmin)
                q = q.Where(e => e.EventManagerId == userId);

            var rawList = await q
                .OrderBy(e => e.StartDate)
                .Select(e => new {
                    eventId = e.EventId,
                    locationId = e.LocationId,
                    locationName = e.Location != null ? e.Location.Name : (e.Title ?? "Event"),
                    title = e.Title,
                    eventCategory = e.Category,
                    category = e.Category,
                    requiresBooking = e.RequiresBooking,
                    startDate = e.StartDate,
                    fromDate = e.StartDate,
                    endDate = e.EndDate,
                    toDate = e.EndDate,
                    startTime = e.StartTime,
                    endTime = e.EndTime,
                    sessionType = e.SessionType,
                    seatingMapImgSrc = e.SeatingMapImgSrc,
                    eventImageImgSrcRaw = e.EventImageImgSrc,
                    description = e.Description,
                    address = e.Address,
                    totalSeats = e.RequiresBooking ? e.TotalSeats : 0,
                    totalSeatsCount = e.RequiresBooking ? e.TotalSeats : 0,
                    availableSeatsCount = e.RequiresBooking
                        ? e.TotalSeats - e.Bookings.Count(b =>
                            b.SeatNumber != null &&
                            b.Status != BookingStatus.Cancelled &&
                            b.Status != BookingStatus.Striked)
                        : 0,
                    createdByEmail = e.EventManager != null ? e.EventManager.Email : null,
                    status = e.Status == EventStatus.Available ? 0
                        : e.Status == EventStatus.Unavailable ? 1
                        : e.Status == EventStatus.Cancelled ? 2
                        : 3,
                    availbleSeatsCount = e.RequiresBooking
                        ? e.TotalSeats - e.Bookings.Count(b =>
                            b.SeatNumber != null &&
                            b.Status != BookingStatus.Cancelled &&
                            b.Status != BookingStatus.Striked)
                        : 0
                })
                .ToListAsync();

            var list = rawList.Select(e =>
            {
                var imageUrls = ParseEventImageUrls(e.eventImageImgSrcRaw);
                return new
                {
                    e.eventId,
                    e.locationId,
                    e.locationName,
                    e.title,
                    e.eventCategory,
                    e.category,
                    e.requiresBooking,
                    e.startDate,
                    e.fromDate,
                    e.endDate,
                    e.toDate,
                    e.startTime,
                    e.endTime,
                    e.sessionType,
                    e.seatingMapImgSrc,
                    eventImageImgSrc = imageUrls.FirstOrDefault(),
                    eventImageImgSrcs = imageUrls,
                    e.description,
                    e.address,
                    e.totalSeats,
                    e.totalSeatsCount,
                    e.availableSeatsCount,
                    e.createdByEmail,
                    e.status,
                    e.availbleSeatsCount
                };
            });

            return Ok(list);
        }

        // POST: api/Events
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        [Authorize(Roles = $"{Roles.EventManager}, {Roles.Admin}")]
        [RequestSizeLimit(30 * 1024 * 1024)] // supports multiple images while still enforcing 4MB per image below
        [RequestFormLimits(MultipartBodyLengthLimit = 30 * 1024 * 1024)]
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

            var requestedCategoryRaw = dto.EventCategory ?? dto.Category;
            var hasCategory = TryParseCategory(requestedCategoryRaw, out var parsedCategory);
            var requiresBooking = dto.RequiresBooking
                ?? (hasCategory ? parsedCategory == EventCategory.Bookable : true);

            if (!hasCategory)
                parsedCategory = requiresBooking ? EventCategory.Bookable : EventCategory.Announcement;
            if (requiresBooking)
                parsedCategory = EventCategory.Bookable;

            var startDate = dto.StartDate ?? dto.FromDate;
            var endDate = dto.EndDate ?? dto.ToDate;
            var availableDate = dto.AvailableDate ?? startDate;
            var startTime = dto.StartTime;
            var endTime = dto.EndTime;

            if (!startDate.HasValue)
                return BadRequest(new { message = "StartDate (or FromDate) is required." });

            endDate ??= startDate;
            availableDate ??= startDate;

            if (endDate.Value < startDate.Value)
                return BadRequest(new { message = "EndDate cannot be earlier than StartDate." });

            if (availableDate.Value > startDate.Value)
                return BadRequest(new { message = ErrorMessages.EventNotAvailable });

            if (requiresBooking)
            {
                if (!dto.LocationId.HasValue)
                    return BadRequest(new { message = "LocationId is required for bookable events." });
                if (!dto.SessionType.HasValue)
                    return BadRequest(new { message = "SessionType is required for bookable events." });
                if (!dto.StartTime.HasValue || !dto.EndTime.HasValue)
                    return BadRequest(new { message = "StartTime and EndTime are required for bookable events." });
                if (!dto.TotalSeats.HasValue || dto.TotalSeats.Value <= 0)
                    return BadRequest(new { message = "TotalSeats must be greater than zero for bookable events." });
            }
            else
            {
                startTime ??= new TimeOnly(0, 0);
                endTime ??= new TimeOnly(23, 59);
            }

            if (dto.LocationId.HasValue)
            {
                var locationExists = await _context.Locations.AnyAsync(l => l.LocationId == dto.LocationId.Value);
                if (!locationExists)
                    return BadRequest(new { message = "Location not found." });
            }

            string? seatingMapUrl = null;
            if (requiresBooking && dto.SeatingMapImage != null)
            {
                try
                {
                    seatingMapUrl = await _azureStorageService.UploadImageAsync(dto.SeatingMapImage);
                }
                catch (Exception ex)
                {
                    return BadRequest(new { message = "Seating map upload failed", detail = ex.Message });
                }
            }

            var uploadedEventImageUrls = new List<string>();
            if (!requiresBooking)
            {
                var eventImages = new List<IFormFile>();
                if (dto.EventImages != null && dto.EventImages.Count > 0)
                {
                    eventImages.AddRange(dto.EventImages.Where(img => img != null && img.Length > 0));
                }
                if (dto.EventImage != null && dto.EventImage.Length > 0)
                {
                    eventImages.Add(dto.EventImage);
                }

                foreach (var image in eventImages)
                {
                    if (image.Length > MaxEventImageBytes)
                    {
                        return BadRequest(new { message = "Each event image must be 4MB or smaller." });
                    }
                }

                foreach (var image in eventImages)
                {
                    try
                    {
                        var uploadedUrl = await _azureStorageService.UploadImageAsync(image);
                        if (!string.IsNullOrWhiteSpace(uploadedUrl))
                            uploadedEventImageUrls.Add(uploadedUrl);
                    }
                    catch (Exception ex)
                    {
                        return BadRequest(new { message = "Event image upload failed", detail = ex.Message });
                    }
                }
            }
            var eventImageStorageValue = SerializeEventImageUrls(uploadedEventImageUrls);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var eventStatus = availableDate.Value <= today ? EventStatus.Available : EventStatus.Unavailable;

            var newEvent = new Event
            {
                LocationId = dto.LocationId,
                EventManagerId = eventManagerId,
                Category = parsedCategory,
                RequiresBooking = requiresBooking,
                Title = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title.Trim(),
                SessionType = dto.SessionType ?? EventSessionType.Adults,
                StartDate = startDate.Value,
                EndDate = endDate.Value,
                StartTime = startTime ?? dto.StartTime!.Value,
                EndTime = endTime ?? dto.EndTime!.Value,
                AvailableDate = availableDate.Value,
                SeatingMapImgSrc = seatingMapUrl,
                EventImageImgSrc = eventImageStorageValue,
                TotalSeats = requiresBooking ? dto.TotalSeats!.Value : 0,
                Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim(),
                Status = eventStatus
            };

            _context.Events.Add(newEvent);
            await _context.SaveChangesAsync();

            return Ok(new { message = SuccessMessages.EventCreateSuccessful, eventId = newEvent.EventId });
        }

        [HttpPut("{id}/cancel")]
        [Authorize(Roles = $"{Roles.EventManager},{Roles.Admin}")]
        public async Task<IActionResult> CancelEvent(int id, [FromBody] CancelEventRequestDto dto, [FromServices] EmailService emailService)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = ErrorMessages.InvalidRequest });

            var ev = await _context.Events
                .Include(e => e.Location)
                .Include(e => e.Bookings)
                    .ThenInclude(b => b.User)
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (ev == null)
                return NotFound(new { message = ErrorMessages.InvalidRequest });

            // Only the manager for the particular event or an admin should be able to cancel
            if (ev.EventManagerId != userId && !User.IsInRole(Roles.Admin))
                return Forbid();


            // Cancel all associated bookings that aren't already cancelled
            foreach (var booking in ev.Bookings.Where(b => b.Status != BookingStatus.Cancelled))
            {
                booking.Status = BookingStatus.Cancelled;

                if (!string.IsNullOrWhiteSpace(booking.User?.Email))
                {
                    // Pretty strings (12-hour am/pm)
                    string eventDatePretty = ev.StartDate.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture);
                    string timeRangePretty = $"{FormatTime12(ev.StartTime)} – {FormatTime12(ev.EndTime)}";

                    string locationName = ev.Location?.Name ?? "TBA";
                    string addressPretty = string.IsNullOrWhiteSpace(ev.Address) 
                        ? "Address TBA"
                        : ev.Address;

                    string sessionType = ev.SessionType.ToString();

                    var appName = "Your Corps";
                    var logoUrl = "https://static.wixstatic.com/media/ff8734_0e11ba81866b4340a9ba8d912f1a5423~mv2.png/v1/fill/w_542,h_112,al_c,q_85,usm_0.66_1.00_0.01,enc_avif,quality_auto/YOURCORPS_THIN%20copy.png";
                    var support = "yourcorps@yourcorps.co.nz";

                    string displayName = booking.User.FirstName ?? booking.User.UserName ?? "there";

                    string htmlBody = EmailEventTemplate.EventCancellationHtml(
                        appName: appName,
                        logoUrl: logoUrl,
                        supportEmail: support,
                        userDisplayName: displayName,
                        eventDatePretty: eventDatePretty,
                        timeRangePretty: timeRangePretty,
                        locationName: locationName,
                        addressPretty: addressPretty,
                        sessionType: sessionType,
                        organiserMessage: string.IsNullOrWhiteSpace(dto.CancellationMessage) ? null : dto.CancellationMessage
                    );

                    await emailService.SendEmailAsync(
                        booking.User.Email,
                        "Your Corps – Event cancelled",
                        htmlBody
                    );
                }
            }

            ev.Status = EventStatus.Cancelled;
            await _context.SaveChangesAsync();

            return Ok(new { message = SuccessMessages.EventCancelSuccessful });
        }

        [HttpPut("{id}/remove-listing")]
        [Authorize(Roles = $"{Roles.EventManager},{Roles.Admin}")]
        public async Task<IActionResult> RemoveListing(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = ErrorMessages.InvalidRequest });

            var ev = await _context.Events.FirstOrDefaultAsync(e => e.EventId == id);
            if (ev == null)
                return NotFound(new { message = ErrorMessages.EventNotFound });

            if (ev.EventManagerId != userId && !User.IsInRole(Roles.Admin))
                return Forbid();

            if (ev.RequiresBooking || ev.Category == EventCategory.Bookable)
            {
                return BadRequest(new
                {
                    message = "Bookable events must be cancelled through the cancel action."
                });
            }

            if (ev.Status == EventStatus.Concluded || ev.Status == EventStatus.Cancelled)
                return Ok(new { message = "Listing already removed." });

            try
            {
                await DeleteEventImagesAsync(ev.EventImageImgSrc);
                ev.EventImageImgSrc = null;
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "Failed to remove listing images from storage.",
                    detail = ex.Message
                });
            }

            ev.Status = EventStatus.Concluded;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Listing removed successfully." });
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

        private static DateTime? GetSuspendedUntil(AppUser user)
        {
            // If there’s no recorded strike date, we can’t compute an until date.
            if (user.DateOfLastStrike is null) return null;

            // 90-day suspension from last strike date (stored as DateOnly).
            var until = user.DateOfLastStrike.Value.ToDateTime(TimeOnly.MinValue).AddDays(90);

            // If already expired, return null so the caller knows it’s no longer active.
            return until.Date > DateTime.Today ? until.Date : (DateTime?)null;
        }

        [HttpPost("{eventId}/waitlist")]
        [Authorize]
        public async Task<IActionResult> AddToWaitlist(int eventId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = ErrorMessages.InvalidRequest });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null) return Unauthorized(new { message = ErrorMessages.InvalidRequest });

            var suspended = user.IsSuspended;
            if (suspended || user.AttendanceStrikeCount >= 3)
            {
                var until = GetSuspendedUntil(user);
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = until is not null
                        ? $"Your account is suspended until {until:yyyy-MM-dd}. You cannot join waitlists."
                        : "Your account is suspended. You cannot join waitlists.",
                    suspensionUntil = until?.ToString("yyyy-MM-dd"),
                    strikes = user.AttendanceStrikeCount
                });
            }

            var already = await _context.Waitlists.AnyAsync(w => w.EventId == eventId && w.UserId == userId);
            if (already) return BadRequest(new { message = ErrorMessages.AlreadyOnWaitlist });

            var ev = await _context.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
            if (ev == null) return NotFound(new { message = ErrorMessages.EventNotFound });
            if (!ev.RequiresBooking)
                return BadRequest(new { message = "Waitlist is only available for bookable events." });

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
                .AsNoTracking()
                .Include(b => b.User)
                .Include(b => b.Child)
                .Where(b => b.EventId == eventId &&
                            b.Status != BookingStatus.Cancelled)   // exclude cancelled bookings
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

        // POST: api/Events/report
        [HttpPost("report")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager}")]
        public async Task<IActionResult> GetEventReport([FromBody] ReportRequestDto dto)
        {
            if (dto.EndDate < dto.StartDate)
                return BadRequest(new { message = "EndDate cannot be earlier than StartDate." });

            var eventsInRange = await _context.Events
                .Include(e => e.Bookings)
                    .ThenInclude(b => b.User)
                .Include(e => e.Bookings)
                    .ThenInclude(b => b.Child)
                .Include(e => e.Location)
                .Where(e =>
                    e.RequiresBooking &&
                    e.StartDate >= dto.StartDate &&
                    e.StartDate <= dto.EndDate)
                .ToListAsync();

            if (!eventsInRange.Any())
                return Ok(new { message = "No bookable events found in the given date range." });

            var totalEvents = eventsInRange.Count;

            // All non-cancelled bookings
            var totalBookings = eventsInRange
                .SelectMany(e => e.Bookings)
                .Count(b => b.Status != BookingStatus.Cancelled);

            // Attended bookings only
            var attendedBookings = eventsInRange
                .SelectMany(e => e.Bookings)
                .Where(b => b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.CheckedOut)
                .ToList();

            var totalTurnout = attendedBookings.Count; // seats actually filled

            // Build out list of unique persons to account for user vs child
            var attendeeKeys = attendedBookings
                .Select(b => b.IsForChild && b.ChildId.HasValue
                    ? $"child-{b.ChildId}"
                    : !string.IsNullOrEmpty(b.UserId) ? $"user-{b.UserId}" : null)
                .Where(k => k != null)
                .ToList();

            var uniqueAttendees = attendeeKeys.Distinct().Count();

            var recurringAttendees = attendeeKeys
                .GroupBy(k => k)
                .Count(g => g.Count() > 1);

            var totalUsers = await _context.Users.CountAsync();

            var averageAttendeesPerEvent = totalEvents > 0 ? (double)totalTurnout / totalEvents : 0;

            var eventsPerLocation = eventsInRange
                .GroupBy(e => e.Location?.Name ?? e.Title ?? "Unassigned")
                .Select(g => new
                {
                    Location = g.Key,
                    Count = g.Count()
                });

            var totalSeats = eventsInRange.Sum(e => e.TotalSeats);
            var attendanceRateOverall = totalSeats > 0 ? (double)totalTurnout / totalSeats : 0;

            var reportDto = new EventReportDto
            {
                TotalEvents = totalEvents,
                TotalUsers = totalUsers,
                TotalBookings = totalBookings,
                TotalTurnout = totalTurnout,
                UniqueAttendees = uniqueAttendees,
                RecurringAttendees = recurringAttendees,
                AverageAttendeesPerEvent = Math.Round(averageAttendeesPerEvent, 2),
                EventsPerLocation = eventsPerLocation.Select(e => new EventsPerLocationDto
                {
                    Location = e.Location,
                    Count = e.Count
                }),
                AttendanceRateOverall = Math.Round(attendanceRateOverall, 2)
            };

            return Ok(reportDto);
        }
    }
}
