using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CorpsAPI.Data;
using CorpsAPI.Models;
using CorpsAPI.DTOs;
using Microsoft.AspNetCore.Authorization;
using CorpsAPI.Constants;

namespace CorpsAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LocationsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public LocationsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Locations
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetLocations()
        {
            var locations = await _context.Locations
                .ToListAsync();

            var dtos = locations.Select(l => new GetAllLocationsDto(l)).ToList();
            return Ok(dtos);
        }

        // GET: api/Locations/5
        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetLocation(int id)
        {
            var location = await _context.Locations
                .FirstOrDefaultAsync();

            if (location == null)
                return NotFound(new { message = "The specified location does not exist." });

            var dto = new GetLocationDto(location);

            return Ok(dto);
        }

        /*// PUT: api/Locations/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> PutLocation(int id, Location location)
        {
            if (id != location.LocationId)
            {
                return BadRequest();
            }

            _context.Entry(location).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!LocationExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }*/

        /*// POST: api/Locations
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        [Authorize(Roles = Roles.Admin)]
        public async Task<ActionResult<Location>> PostLocation(Location location)
        {
            _context.Locations.Add(location);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetLocation", new { id = location.LocationId }, location);
        }*/

       /* // DELETE: api/Locations/5
        [HttpDelete("{id}")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> DeleteLocation(int id)
        {
            var location = await _context.Locations.FindAsync(id);
            if (location == null)
            {
                return NotFound();
            }

            _context.Locations.Remove(location);
            await _context.SaveChangesAsync();

            return NoContent();
        }*/

        /*private bool LocationExists(int id)
        {
            return _context.Locations.Any(e => e.LocationId == id);
        }*/
    }
}
