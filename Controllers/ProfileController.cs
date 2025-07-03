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
    public class ProfileController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        public ProfileController(UserManager<AppUser> userManager)
        {
            _userManager = userManager;
        }

        [HttpGet("profile")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager},{Roles.Staff},{Roles.User}")]
        public async Task<IActionResult> GetProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound(new { message = ErrorMessages.InvalidRequest });

            var dto = new UserProfileDto
            {
                UserName = user.UserName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Age = user.Age
            };

            return Ok(dto);
        }

        [HttpPatch("profile")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager},{Roles.Staff},{Roles.User}")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequestDto dto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound(new { message = ErrorMessages.InvalidRequest });

            if (!string.IsNullOrEmpty(dto.NewUserName) && dto.NewUserName != user.UserName)
            {
                var userNameExists = await _userManager.FindByNameAsync(dto.NewUserName);
                if (userNameExists != null)
                    return BadRequest(new { message = ErrorMessages.UserNameTaken });

                user.UserName = dto.NewUserName;
            }

            // only update if provided
            user.FirstName = dto.NewFirstName ?? user.FirstName;
            user.LastName = dto.NewLastName ?? user.LastName;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                var errorMessages = string.Join(",\n", result.Errors.Select(e => e.Description));
                return BadRequest(new { message = "Update failed:\n" + errorMessages });
            }

            return Ok(new { message = SuccessMessages.ProfileUpdateSuccessful });
        }

        [HttpDelete("profile")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager},{Roles.Staff},{Roles.User}")]
        public async Task<IActionResult> DeleteProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound(new { message = ErrorMessages.InvalidRequest });

            var result = await _userManager.DeleteAsync(user);

            if (!result.Succeeded)
                return BadRequest(new { message = ErrorMessages.InvalidRequest });

            return Ok(new { message = SuccessMessages.ProfileDeleteSuccessful });
        }
    }
}
