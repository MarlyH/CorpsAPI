// CorpsAPI/Controllers/LocationsController.cs
using System.Security.Claims;
using CorpsAPI.Constants;
using CorpsAPI.Data;
using CorpsAPI.DTOs;
using CorpsAPI.Models;
using CorpsAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CorpsAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LocationsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly AzureStorageService _azureStorageService;

        public LocationsController(
            AppDbContext context,
            AzureStorageService azureStorageService)
        {
            _context               = context;
            _azureStorageService   = azureStorageService;
        }

        // GET: api/Locations
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetLocations()
        {
            var locations = await _context.Locations
                .ToListAsync();

            var dtos = locations
                .Select(l => new GetAllLocationsDto(l))
                .ToList();

            return Ok(dtos);
        }

        // GET: api/Locations/5
        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetLocation(int id)
        {
            var location = await _context.Locations
                .FirstOrDefaultAsync(l => l.LocationId == id);

            if (location == null)
                return NotFound(new { message = "The specified location does not exist." });

            var dto = new GetLocationDto(location);
            return Ok(dto);
        }

        // POST: api/Locations
        [HttpPost]
        [Authorize(Roles = Roles.Admin)]
        [RequestSizeLimit(5 * 1024 * 1024)]   // e.g. 5 MB max
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreateLocation([FromForm] CreateLocationDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            string? imageUrl = null;
            if (dto.MascotImage != null)
            {
                try
                {
                    imageUrl = await _azureStorageService
                        .UploadImageAsync(dto.MascotImage);
                }
                catch (Exception ex)
                {
                    return BadRequest(new { message = "Image upload failed", detail = ex.Message });
                }
            }

            var location = new Location
            {
                Name         = dto.Name,
                MascotImgSrc = imageUrl
            };

            _context.Locations.Add(location);
            await _context.SaveChangesAsync();

            var resultDto = new GetLocationDto(location);
            return CreatedAtAction(
                nameof(GetLocation),
                new { id = location.LocationId },
                resultDto);
        }

        // PUT: api/Locations/5
        [HttpPut("{id}")]
        [Authorize(Roles = Roles.Admin)]
        [RequestSizeLimit(5 * 1024 * 1024)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateLocation(
            int id,
            [FromForm] UpdateLocationDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var location = await _context.Locations
                .FirstOrDefaultAsync(l => l.LocationId == id);

            if (location == null)
                return NotFound(new { message = "Location not found." });

            location.Name = dto.Name;

            if (dto.MascotImage != null)
            {
                // Optional: delete old blob if you track blob name separately

                try
                {
                    var newUrl = await _azureStorageService
                        .UploadImageAsync(dto.MascotImage);
                    location.MascotImgSrc = newUrl;
                }
                catch (Exception ex)
                {
                    return BadRequest(new { message = "Image upload failed", detail = ex.Message });
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new GetLocationDto(location));
        }

        // DELETE: api/Locations/5
        [HttpDelete("{id}")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> DeleteLocation(int id)
        {
            var location = await _context.Locations
                .Include(l => l.Events)
                .FirstOrDefaultAsync(l => l.LocationId == id);

            if (location == null)
                return NotFound(new { message = "Location not found." });

            if (location.Events.Any())
                return BadRequest(new { message = "Cannot delete a location with existing events." });

            // Optional: delete the mascot image from blob storage
            if (!string.IsNullOrEmpty(location.MascotImgSrc))
            {
                await _azureStorageService.DeleteImageAsync(location.MascotImgSrc);
            }

            _context.Locations.Remove(location);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
