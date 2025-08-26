using System.Globalization;
using CorpsAPI.Constants;
using CorpsAPI.DTOs.Profile;
using CorpsAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CorpsAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserManagementController : ControllerBase
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<AppUser> _userManager;
        public UserManagementController(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [HttpPost("change-role")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager}")]
        public async Task<IActionResult> ChangeUserRole([FromBody] ChangeUserRole dto)
        {
            // normalise (user => User)
            dto.Role = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(dto.Role.ToLower());

            // event managers can only change the roles of users/staff
            if (User.IsInRole(Roles.EventManager)
                && dto.Role != Roles.Staff
                && dto.Role != Roles.User)
            {
                return BadRequest(new { message = ErrorMessages.EventManagerRestrictions });
            }

            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                return NotFound("User not found");

            // ensure role actually exists
            if (!await _roleManager.RoleExistsAsync(dto.Role))
                return BadRequest(new { message = ErrorMessages.RoleNotExist });

            // get old roles
            var currentRoles = await _userManager.GetRolesAsync(user);

            // don't let admins be demoted
            if (currentRoles.Contains(Roles.Admin))
                return BadRequest(new { message = ErrorMessages.CannotDemoteAdmin });

            // no change if the user already has only the target role
            if (currentRoles.Count == 1 && currentRoles.Contains(dto.Role))
                return BadRequest(new { message = ErrorMessages.UserAlreadyInRole });

            // add user to new role
            var resultAdd = await _userManager.AddToRoleAsync(user, dto.Role);
            if (!resultAdd.Succeeded)
                return BadRequest(new { message = ErrorMessages.AddToRoleFailed });

            // remove from old roles
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
                return BadRequest(new { message = ErrorMessages.RemoveFromExistingRoles });

            return Ok(new { message = SuccessMessages.ChangeRoleSuccess });
        }

        // self-appeal: uses IsSuspended, clears strikes only if currently suspended
        [HttpPost("ban-appeal/self")]
        [Authorize(Roles = $"{Roles.User},{Roles.Staff},{Roles.Admin},{Roles.EventManager}")]
        public async Task<IActionResult> AppealBanForSelf()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized(new { message = ErrorMessages.InvalidRequest });

            // this will auto-clear if 90 days have passed, using app user model
            var suspended = user.IsSuspended;

            if (!suspended)
                return BadRequest(new { message = "No active suspension to appeal." });

            user.AttendanceStrikeCount = 0;
            user.DateOfLastStrike = null;

            var res = await _userManager.UpdateAsync(user);
            if (!res.Succeeded)
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Failed to clear suspension." });

            return Ok(new { message = "Suspension cleared.", isSuspended = false });
        }

        // admin / event manager: clear a specific user's suspension
        [HttpPost("ban-appeal/for-user")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager}")]
        public async Task<IActionResult> AppealBanForUser([FromBody] BanAppealRequest dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest(new { message = "Email is required." });

            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                return NotFound(new { message = "User not found." });

            // triggers app user models 90-day auto-clear if it’s time
            var suspended = user.IsSuspended;

            if (!suspended)
                return BadRequest(new { message = "User has no active suspension." });

            user.AttendanceStrikeCount = 0;
            user.DateOfLastStrike = null;

            var res = await _userManager.UpdateAsync(user);
            if (!res.Succeeded)
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Failed to clear suspension." });

            return Ok(new { message = $"Suspension cleared for {dto.Email}." });
        }

        [HttpPost("unban/{userId}")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager}")]
        public async Task<IActionResult> Unban(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound(new { message = "User not found." });

            user.AttendanceStrikeCount = 0;
            user.DateOfLastStrike = null;
            var res = await _userManager.UpdateAsync(user);
            if (!res.Succeeded) return StatusCode(500, new { message = "Failed to clear strikes." });

            return Ok(new { message = "Strikes cleared." });
        }



        // list currently suspended users
        [HttpGet("banned-users")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager}")]
        public async Task<IActionResult> GetBannedUsers()
        {
            // fetch likely candidates only; IsSuspended will auto-clear if window passed
            var candidates = await _userManager.Users
                .Where(u => u.AttendanceStrikeCount >= 3 && u.DateOfLastStrike != null)
                .ToListAsync();

            var now = DateTime.Today;

            var banned = new List<BannedUserDto>();

            foreach (var u in candidates)
            {
                var wasSuspended = u.IsSuspended; // may auto-reset if 90+ days elapsed

                if (wasSuspended)
                {
                    var until = u.DateOfLastStrike?.ToDateTime(TimeOnly.MinValue).AddDays(90);
                    banned.Add(new BannedUserDto
                    {
                        Id = u.Id,
                        Email = u.Email!,
                        FirstName = u.FirstName,
                        LastName = u.LastName,
                        AttendanceStrikeCount = u.AttendanceStrikeCount,
                        DateOfLastStrike = u.DateOfLastStrike,
                        SuspensionUntil = until
                    });

                }
                else
                {
                    // IsSuspended may have cleared strikes; persist that
                    await _userManager.UpdateAsync(u);
                }
            }

            // soonest to expire first
            banned = banned.OrderBy(b => b.SuspensionUntil ?? now).ToList();
            return Ok(banned);
        }

    }
}
