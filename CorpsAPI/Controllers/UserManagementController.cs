using System.Globalization;
using CorpsAPI.Constants;
using CorpsAPI.DTOs.Profile;
using CorpsAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CorpsAPI.Data;

namespace CorpsAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserManagementController : ControllerBase
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<AppUser> _userManager;
        private readonly AppDbContext _context;
        public UserManagementController(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager, AppDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context; 
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

        // get lists of account users

        [HttpGet("users")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager}")]
        public async Task<IActionResult> GetAllUsers(
            [FromQuery] string? q = null, // optional search on name/email
            [FromQuery] int page = 1,// optional paging (1-based)
            [FromQuery] int pageSize = 50) // optional page size
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 1;
            if (pageSize > 200) pageSize = 200; // cap to avoid huge responses

            var query = _userManager.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                query = query.Where(u =>
                    (u.FirstName + " " + u.LastName).ToLower().Contains(term) ||
                    (u.Email ?? "").ToLower().Contains(term));
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserMiniDto
                {
                    Id = u.Id,
                    Email = u.Email ?? string.Empty,
                    FirstName = u.FirstName,
                    LastName = u.LastName
                })
                .ToListAsync();

            return Ok(new
            {
                total,
                page,
                pageSize,
                items
            });
        }
        // POST: /api/UserManagement/users/by-ids
        // Body: { "ids": ["<id1>", "<id2>", ...] }
        // Bulk lookup to resolve many userIds -> names/strike info in one call
        [HttpPost("users/by-ids")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager}")]
        public async Task<IActionResult> GetUsersByIds([FromBody] UsersByIdsRequest req)
        {
            if (req?.Ids == null || req.Ids.Count == 0)
                return BadRequest(new { message = "Provide at least one Id." });

            // Fetch users in one query
            var users = await _userManager.Users
                .Where(u => req.Ids.Contains(u.Id))
                .ToListAsync();

            // Build map of Id -> roles to avoid N+1 where possible
            // (Identity doesn't expose a direct batch API; we’ll fetch per-user here.)
            var results = new List<UserSummaryDto?>(users.Count);

            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);

                // Run auto-clear check, persist if changed
                var _ = u.IsSuspended;
                await _userManager.UpdateAsync(u);

                results.Add(new UserSummaryDto
                {
                    Id = u.Id,
                    Email = u.Email ?? string.Empty,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Roles = roles.ToList(),
                    AttendanceStrikeCount = u.AttendanceStrikeCount,
                    DateOfLastStrike = u.DateOfLastStrike,
                    IsSuspended = u.IsSuspended
                });
            }

            // Maintain input order if you want:
            results = req.Ids
                .Select(id => results.FirstOrDefault(r => r?.Id == id))
                .ToList();
                

            return Ok(results);
        }

        // GET: /api/UserManagement/user/{userId}/children
        [HttpGet("user/{userId}/children")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager}")]
        public async Task<IActionResult> GetChildrenForUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return BadRequest(new { message = "UserId is required." });

            // Load user with children in one query
            var user = await _userManager.Users
                .Include(u => u.Children)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null)
                return NotFound(new { message = "User not found." });

            var result = user.Children
                .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
                .Select(c => new DTOs.Child.ChildDto
                {
                    ChildId = c.ChildId,
                    FirstName = c.FirstName,
                    LastName  = c.LastName,
                    DateOfBirth = c.DateOfBirth,
                    EmergencyContactName = c.EmergencyContactName,
                    EmergencyContactPhone = c.EmergencyContactPhone,
                    Age = CalculateAge(c.DateOfBirth)
                })
                .ToList();

            return Ok(result);
        }

        private static int CalculateAge(DateOnly dob)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var age = today.Year - dob.Year;
            if (today.Month < dob.Month || (today.Month == dob.Month && today.Day < dob.Day))
                age--;
            return age;
        }

        [HttpPost("strikes/increment")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager}")]
        public async Task<IActionResult> IncrementStrikes([FromBody] StrikeAdjustByIdDto dto)
        {
            if (dto is null || string.IsNullOrWhiteSpace(dto.UserId))
                return BadRequest(new { message = "UserId is required." });

            var amount = dto.Amount <= 0 ? 1 : dto.Amount; // default to +1
            return await AdjustStrikesInternal(dto.UserId, +amount);
        }

        [HttpPost("strikes/decrement")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager}")]
        public async Task<IActionResult> DecrementStrikes([FromBody] StrikeAdjustByIdDto dto)
        {
            if (dto is null || string.IsNullOrWhiteSpace(dto.UserId))
                return BadRequest(new { message = "UserId is required." });

            var amount = dto.Amount <= 0 ? 1 : dto.Amount; // default to -1
            return await AdjustStrikesInternal(dto.UserId, -amount);
        }

        [HttpPost("strikes/set")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager}")]
        public async Task<IActionResult> SetStrikes([FromBody] StrikeSetByIdDto dto)
        {
            if (dto is null || string.IsNullOrWhiteSpace(dto.UserId))
                return BadRequest(new { message = "UserId is required." });
            if (dto.Count < 0)
                return BadRequest(new { message = "Count must be >= 0." });

            var user = await _userManager.FindByIdAsync(dto.UserId);
            if (user is null)
                return NotFound(new { message = "User not found." });

            // Event Managers cannot modify Admin users
            var roles = await _userManager.GetRolesAsync(user);
            if (User.IsInRole(Roles.EventManager) && roles.Contains(Roles.Admin))
                return Forbid("Event Managers cannot modify Admin users.");

            var previous = user.AttendanceStrikeCount;
            user.AttendanceStrikeCount = dto.Count;

            if (dto.Count == 0)
            {
                user.DateOfLastStrike = null;
            }
            else if (dto.Count > previous) // strikes increased
            {
                user.DateOfLastStrike = TodayInNZT();
            }
            // else (reduced but still > 0): keep existing DateOfLastStrike as most-recent strike date

            var res = await _userManager.UpdateAsync(user);
            if (!res.Succeeded)
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to update strikes." });

            return Ok(new
            {
                message = "Strike count set.",
                user = new
                {
                    id = user.Id,
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    attendanceStrikeCount = user.AttendanceStrikeCount,
                    dateOfLastStrike = user.DateOfLastStrike,
                    isSuspended = user.IsSuspended
                }
            });
        }
        

        private static DateOnly TodayInNZT()
        {
            // Windows: "New Zealand Standard Time"
            // Linux (if needed): "Pacific/Auckland"
            var tzId = "New Zealand Standard Time";
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
                var nowNzt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                return DateOnly.FromDateTime(nowNzt);
            }
            catch (TimeZoneNotFoundException)
            {
                // Fallback for Linux containers
                var tz = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");
                var nowNzt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                return DateOnly.FromDateTime(nowNzt);
            }
        }

        private async Task<IActionResult> AdjustStrikesInternal(string userId, int delta)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
                return NotFound(new { message = "User not found." });

            var roles = await _userManager.GetRolesAsync(user);
            if (User.IsInRole(Roles.EventManager) && roles.Contains(Roles.Admin))
                return Forbid("Event Managers cannot modify Admin users.");

            var previous = user.AttendanceStrikeCount;
            var next = previous + delta;
            if (next < 0) next = 0;

            user.AttendanceStrikeCount = next;

            if (delta > 0) // added strikes
            {
                user.DateOfLastStrike = TodayInNZT();
            }
            else if (next == 0) // cleared all strikes
            {
                user.DateOfLastStrike = null;
            }
            // else (reduced but still > 0): keep last strike date

            var res = await _userManager.UpdateAsync(user);
            if (!res.Succeeded)
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to update strikes." });

            return Ok(new
            {
                message = delta > 0 ? $"Added {delta} strike(s)." : $"Removed {Math.Abs(delta)} strike(s).",
                user = new {
                    id = user.Id,
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    attendanceStrikeCount = user.AttendanceStrikeCount,
                    dateOfLastStrike = user.DateOfLastStrike,
                    isSuspended = user.IsSuspended
                }
            });
        }

    }
    
}
