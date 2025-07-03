using CorpsAPI.Constants;
using CorpsAPI.DTOs.Child;
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

            List<Child> children;

            if (roles.Contains(Roles.Admin) || roles.Contains(Roles.EventManager))
            {
                // Admins and EventManagers get all children
                children = await _context.Children.ToListAsync();
            }
            else
            {
                // Regular users and staff only get their own
                children = await _context.Children
                    .Where(c => c.ParentUserId == user.Id)
                    .ToListAsync();
            }

            var result = children.Select(c => new ChildDto
            {
                ChildId = c.ChildId,
                FirstName = c.FirstName,
                LastName = c.LastName,
                DateOfBirth = c.DateOfBirth,
                EmergencyContactName = c.EmergencyContactName,
                EmergencyContactPhone = c.EmergencyContactPhone,
                Age = c.Age
            });

            return Ok(result);
        }


        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var roles = await _userManager.GetRolesAsync(user);

            Child? child;

            if (roles.Contains(Roles.Admin) || roles.Contains(Roles.EventManager))
            {
                // Admin/EventManager can fetch any child
                child = await _context.Children.FirstOrDefaultAsync(c => c.ChildId == id);
            }
            else
            {
                // Regular users/staff must own the child
                child = await _context.Children
                    .FirstOrDefaultAsync(c => c.ChildId == id && c.ParentUserId == user.Id);
            }

            if (child == null)
                return NotFound(new { message = "Child not found." });

            return Ok(new ChildDto
            {
                ChildId = child.ChildId,
                FirstName = child.FirstName,
                LastName = child.LastName,
                DateOfBirth = child.DateOfBirth,
                EmergencyContactName = child.EmergencyContactName,
                EmergencyContactPhone = child.EmergencyContactPhone,
                Age = child.Age
            });
        }


        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateChildDto dto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

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
            await _context.SaveChangesAsync();

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

            child.FirstName = dto.FirstName;
            child.LastName = dto.LastName;
            child.DateOfBirth = dto.DateOfBirth;
            child.EmergencyContactName = dto.EmergencyContactName;
            child.EmergencyContactPhone = dto.EmergencyContactPhone;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Child updated successfully." });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var child = await _context.Children.FirstOrDefaultAsync(c => c.ChildId == id && c.ParentUserId == user.Id);
            if (child == null)
                return NotFound(new { message = "Child not found." });

            _context.Children.Remove(child);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Child deleted successfully." });
        }
    }
}
