using System.Globalization;
using CorpsAPI.Constants;
using CorpsAPI.DTOs.Profile;
using CorpsAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

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
    }
}
