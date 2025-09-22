using CorpsAPI.Constants;
using CorpsAPI.DTOs.Child;
using CorpsAPI.DTOs;
using CorpsAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CorpsAPI.Data;

namespace CorpsAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = $"{Roles.User},{Roles.Admin},{Roles.Staff},{Roles.EventManager}")]
    public class ChildController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public ChildController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var roles = await _userManager.GetRolesAsync(user);

            IQueryable<Child> q = _context.Children.AsNoTracking();
            if (!(roles.Contains(Roles.Admin) || roles.Contains(Roles.EventManager)))
                q = q.Where(c => c.ParentUserId == user.Id);

            var children = await q
                .Select(c => new {
                    Child = c,
                    Meds = _context.ChildMedicalConditions
                        .Where(m => m.ChildId == c.ChildId)
                        .Select(m => new MedicalConditionDto {
                            Id = m.Id, Name = m.Name, Notes = m.Notes, IsAllergy = m.IsAllergy
                        })
                        .ToList()
                })
                .ToListAsync();

            var result = children.Select(x => new ChildDto
            {
                ChildId = x.Child.ChildId,
                FirstName = x.Child.FirstName,
                LastName = x.Child.LastName,
                DateOfBirth = x.Child.DateOfBirth,
                EmergencyContactName = x.Child.EmergencyContactName,
                EmergencyContactPhone = x.Child.EmergencyContactPhone,
                Age = x.Child.Age,
                HasMedicalConditions = x.Meds.Count > 0,
                MedicalConditions = x.Meds
            });

            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var roles = await _userManager.GetRolesAsync(user);

            IQueryable<Child> q = _context.Children.AsNoTracking();
            if (roles.Contains(Roles.Admin) || roles.Contains(Roles.EventManager))
                q = q.Where(c => c.ChildId == id);
            else
                q = q.Where(c => c.ChildId == id && c.ParentUserId == user.Id);

            var child = await q.FirstOrDefaultAsync();
            if (child == null)
                return NotFound(new { message = "Child not found." });

            var meds = await _context.ChildMedicalConditions
                .AsNoTracking()
                .Where(m => m.ChildId == child.ChildId)
                .OrderByDescending(m => m.IsAllergy)
                .ThenBy(m => m.Name)
                .Select(m => new MedicalConditionDto {
                    Id = m.Id, Name = m.Name, Notes = m.Notes, IsAllergy = m.IsAllergy
                })
                .ToListAsync();

            return Ok(new ChildDto
            {
                ChildId = child.ChildId,
                FirstName = child.FirstName,
                LastName = child.LastName,
                DateOfBirth = child.DateOfBirth,
                EmergencyContactName = child.EmergencyContactName,
                EmergencyContactPhone = child.EmergencyContactPhone,
                Age = child.Age,
                HasMedicalConditions = meds.Count > 0,
                MedicalConditions = meds
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateChildDto dto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // If toggle is true, ensure at least one condition provided
            if (dto.HasMedicalConditions && (dto.MedicalConditions == null || dto.MedicalConditions.Count == 0))
                return BadRequest(new { message = "Please add at least one medical condition or set the toggle to No." });

            var child = new Child
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                DateOfBirth = dto.DateOfBirth,
                EmergencyContactName = dto.EmergencyContactName,
                EmergencyContactPhone = dto.EmergencyContactPhone,
                ParentUserId = user.Id
            };

            _context.Children.Add(child);
            await _context.SaveChangesAsync(); // need ChildId

            // Insert medical conditions if any
            if (dto.HasMedicalConditions && dto.MedicalConditions != null)
            {
                var rows = dto.MedicalConditions
                    .Where(m => !string.IsNullOrWhiteSpace(m.Name))
                    .Select(m => new ChildMedicalCondition
                    {
                        ChildId = child.ChildId,
                        Name = m.Name.Trim(),
                        Notes = m.Notes?.Trim(),
                        IsAllergy = m.IsAllergy
                    });

                _context.ChildMedicalConditions.AddRange(rows);
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "Child created successfully.", childId = child.ChildId });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateChildDto dto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var child = await _context.Children.FirstOrDefaultAsync(c => c.ChildId == id && c.ParentUserId == user.Id);
            if (child == null)
                return NotFound(new { message = "Child not found." });

            if (dto.HasMedicalConditions && (dto.MedicalConditions == null || dto.MedicalConditions.Count == 0))
                return BadRequest(new { message = "Please add at least one medical condition or set the toggle to No." });

            child.FirstName = dto.FirstName;
            child.LastName = dto.LastName;
            child.DateOfBirth = dto.DateOfBirth;
            child.EmergencyContactName = dto.EmergencyContactName;
            child.EmergencyContactPhone = dto.EmergencyContactPhone;

            // Replace-all semantics for simplicity:
            var existing = await _context.ChildMedicalConditions
                .Where(m => m.ChildId == child.ChildId)
                .ToListAsync();
            if (existing.Count > 0)
                _context.ChildMedicalConditions.RemoveRange(existing);

            if (dto.HasMedicalConditions && dto.MedicalConditions != null)
            {
                var rows = dto.MedicalConditions
                    .Where(m => !string.IsNullOrWhiteSpace(m.Name))
                    .Select(m => new ChildMedicalCondition
                    {
                        ChildId = child.ChildId,
                        Name = m.Name.Trim(),
                        Notes = m.Notes?.Trim(),
                        IsAllergy = m.IsAllergy
                    });
                _context.ChildMedicalConditions.AddRange(rows);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Child updated successfully." });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var child = await _context.Children
                .FirstOrDefaultAsync(c => c.ChildId == id && c.ParentUserId == user.Id);

            if (child == null)
                return NotFound(new { message = "Child not found." });

            // cancel bookings (your existing logic)
            var activeBookings = await _context.Bookings
                .Where(b => b.IsForChild && b.ChildId == id && b.Status != BookingStatus.Cancelled)
                .ToListAsync();

            foreach (var b in activeBookings)
            {
                b.Status = BookingStatus.Cancelled;
                b.SeatNumber = null;

                if (string.IsNullOrWhiteSpace(b.ReservedBookingAttendeeName))
                    b.ReservedBookingAttendeeName = $"{child.FirstName} {child.LastName}";

                b.ChildId = null;
                b.IsForChild = false;
            }

            _context.Children.Remove(child); // Ensure cascade delete is configured (see note below)
            await _context.SaveChangesAsync();

            return Ok(new { message = "Child deleted successfully. Associated bookings were cancelled and seats freed." });
        }
    }
}
