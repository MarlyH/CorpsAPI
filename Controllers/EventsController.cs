using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CorpsAPI.Constants;
using CorpsAPI.Data;
using CorpsAPI.DTOs;
using CorpsAPI.Models;
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

        public EventsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Events
        [HttpGet]
        public async Task<IActionResult> GetEvents()
        {
            var events = await _context.Events
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
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (ev == null)
                return NotFound(new { message = "Event not found." });

            var dto = new GetEventDto(ev);

            return Ok(dto);
        }


        // POST: api/Events
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        [Authorize(Roles = $"{Roles.EventManager}, {Roles.Admin}")]
        public async Task<IActionResult> PostEvent([FromBody] CreateEventDto dto)
        {
            var eventManagerId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(eventManagerId))
            {
                return Unauthorized(new { message = "User ID not found in token." });
            }

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
                return BadRequest(new { message = "The event must be available before it starts." });

            var newEvent = new Event
            {
                LocationId = dto.LocationId,
                EventManagerId = eventManagerId,
                SessionType = dto.SessionType,
                StartDate = dto.StartDate,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                AvailableDate = dto.AvailableDate,
                SeatingMapImgSrc = dto.SeatingMapImgSrc,
                TotalSeats = dto.TotalSeats,
                Description = dto.Description,
                Address = dto.Address
            };

            _context.Events.Add(newEvent);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Event successfully created." });
        }

        /*// DELETE: api/Events/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            // event cancellation flow here
        }*/
    }
}
