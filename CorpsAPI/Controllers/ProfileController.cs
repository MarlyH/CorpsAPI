using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CorpsAPI.Constants;
using CorpsAPI.Data;
using CorpsAPI.DTOs.Profile;
using CorpsAPI.DTOs;
using CorpsAPI.Models;

namespace CorpsAPI.Controllers
{
    [ApiController]
    [Route("api/profile")]
    [Authorize(Roles = $"{Roles.User},{Roles.Admin},{Roles.Staff},{Roles.EventManager}")]
    public class ProfileController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly AppDbContext _context;

        public ProfileController(UserManager<AppUser> userManager, AppDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        /// <summary>
        /// Get the current user’s profile.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound(new { message = ErrorMessages.InvalidRequest });

            // Trigger auto-clear side-effect if 90 days passed
            var isSuspended = user.IsSuspended;
            if (!isSuspended && user.AttendanceStrikeCount == 0 && user.DateOfLastStrike == null)
            {
                // persist cleared state, in case IsSuspended reset strikes
                await _userManager.UpdateAsync(user);
            }

            DateTime? suspensionUntil = null;
            if (isSuspended && user.DateOfLastStrike is not null)
            {
                var until = user.DateOfLastStrike.Value.ToDateTime(TimeOnly.MinValue).AddDays(90);
                if (until.Date > DateTime.Today) suspensionUntil = until.Date;
            }

            var dto = new UserProfileDto
            {
                UserName = user.UserName!,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email!,
                Age = user.Age,
                IsSuspended = user.IsSuspended,
                AttendanceStrikeCount = user.AttendanceStrikeCount,
                PhoneNumber = user.PhoneNumber,
                SuspensionUntil = suspensionUntil
            };

            return Ok(dto);
        }

        /// <summary>
        /// Update username, first name, or last name.
        /// </summary>
        [HttpPatch]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequestDto dto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound(new { message = ErrorMessages.InvalidRequest });

            // Change username if provided
            if (!string.IsNullOrWhiteSpace(dto.NewUserName) && dto.NewUserName != user.UserName)
            {
                if (await _userManager.FindByNameAsync(dto.NewUserName) != null)
                    return BadRequest(new { message = ErrorMessages.UserNameTaken });
                user.UserName = dto.NewUserName;
            }

            // Change first/last name if provided
            user.FirstName = dto.NewFirstName ?? user.FirstName;
            user.LastName = dto.NewLastName ?? user.LastName;
            user.PhoneNumber = dto.NewPhoneNumber ?? user.PhoneNumber;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                return BadRequest(new { message = $"Update failed: {errors}" });
            }

            return Ok(new { message = SuccessMessages.ProfileUpdateSuccessful });
        }

        /// <summary>
        /// Hard-delete any user (Admin/EventManager only).
        /// </summary>
        [HttpDelete]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager}")]
        public async Task<IActionResult> DeleteProfile([FromQuery] string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound(new { message = ErrorMessages.InvalidRequest });

            // Cancel & orphan bookings
            var bookings = await _context.Bookings
                .Include(b => b.Child)
                .Where(b =>
                    b.UserId == user.Id ||
                    (b.IsForChild && b.Child.ParentUserId == user.Id))
                .ToListAsync();

            bookings.ForEach(b =>
            {
                b.Status = BookingStatus.Cancelled;
                b.UserId = null;
                b.ChildId = null;
            });
            await _context.SaveChangesAsync();

            // Remove their children
            var children = await _context.Children
                .Where(c => c.ParentUserId == user.Id)
                .ToListAsync();
            _context.Children.RemoveRange(children);
            await _context.SaveChangesAsync();

            // Finally delete the account
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                return StatusCode(500, new { message = $"Failed to delete user: {errors}" });
            }

            return Ok(new { message = SuccessMessages.ProfileDeleteSuccessful });
        }


        /// <summary>
        /// Self-delete: cancel your bookings, remove your children, then delete yourself.
        /// </summary>
        [HttpDelete("me")]
        public async Task<IActionResult> DeleteMyProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized(new { message = ErrorMessages.InvalidRequest });

            // wrap in a transaction
            await using var tx = await _context.Database.BeginTransactionAsync();

            // Cancel & orphan all bookings for this user or their children
            var bookings = await _context.Bookings
                .Include(b => b.Child)
                .Where(b =>
                    b.UserId == user.Id ||
                    (b.IsForChild && b.Child.ParentUserId == user.Id))
                .ToListAsync();

            bookings.ForEach(b =>
            {
                b.Status = BookingStatus.Cancelled;
                b.UserId = null;
                b.ChildId = null;
            });
            await _context.SaveChangesAsync();

            // Delete all their children
            var children = await _context.Children
                .Where(c => c.ParentUserId == user.Id)
                .ToListAsync();

            _context.Children.RemoveRange(children);
            await _context.SaveChangesAsync();

            // Delete the user account
            var deleteResult = await _userManager.DeleteAsync(user);
            if (!deleteResult.Succeeded)
            {
                await tx.RollbackAsync();
                var errors = string.Join("; ", deleteResult.Errors.Select(e => e.Description));
                return StatusCode(500, new { message = $"Failed to delete account: {errors}" });
            }

            await tx.CommitAsync();
            return Ok(new { message = SuccessMessages.ProfileDeleteSuccessful });
        }
        [HttpGet("medical")]
        public async Task<IActionResult> GetMyMedical()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var items = await _context.UserMedicalConditions
                .Where(m => m.UserId == user.Id)
                .Select(m => new MedicalConditionDto { Name = m.Name, Notes = m.Notes })
                .ToListAsync();

            return Ok(new {
                hasMedicalConditions = items.Count > 0,
                medicalConditions = items
            });
        }

        public class UpsertMedicalRequest
        {
            public bool HasMedicalConditions { get; set; }
            public List<MedicalConditionDto>? MedicalConditions { get; set; }
        }

        [HttpPut("medical")]
        public async Task<IActionResult> UpsertMyMedical([FromBody] UpsertMedicalRequest dto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var existing = await _context.UserMedicalConditions
                .Where(m => m.UserId == user.Id)
                .ToListAsync();

            _context.UserMedicalConditions.RemoveRange(existing);

            if (dto.HasMedicalConditions && dto.MedicalConditions is not null && dto.MedicalConditions.Count > 0)
            {
                var rows = dto.MedicalConditions
                    .Where(m => !string.IsNullOrWhiteSpace(m.Name))
                    .Select(m => new UserMedicalCondition {
                        UserId = user.Id,
                        Name = m.Name.Trim(),
                        Notes = string.IsNullOrWhiteSpace(m.Notes) ? null : m.Notes!.Trim()
                    })
                    .ToList();

                _context.UserMedicalConditions.AddRange(rows);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Medical details updated." });
        }

    }
}
