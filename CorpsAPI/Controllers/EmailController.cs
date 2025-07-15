using System.Net;
using CorpsAPI.Constants;
using CorpsAPI.DTOs.Profile;
using CorpsAPI.Models;
using CorpsAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CorpsAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmailController : ControllerBase
    {
        private readonly EmailService _emailService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _configuration;

        public EmailController(EmailService emailService, UserManager<AppUser> userManager, IConfiguration configuration)
        {
            _emailService = emailService;
            _userManager = userManager;
            _configuration = configuration;
        }

        [Authorize]
        [HttpPost("request-email-change")]
        public async Task<IActionResult> RequestEmailChange([FromBody] ChangeEmailRequestDto dto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized(new { message = ErrorMessages.InvalidRequest });

            // Ensure requested address isn't taken
            var existingUser = await _userManager.FindByEmailAsync(dto.NewEmail);
            if (existingUser != null)
                return BadRequest(new { message = ErrorMessages.EmailTaken });

            var token = await _userManager.GenerateChangeEmailTokenAsync(user, dto.NewEmail);
            var encodedToken = WebUtility.UrlEncode(token);

            var serverUrl = _configuration["ServerUrl"];
            var url = $"{serverUrl}/api/email/confirm-email-change?userId={user.Id}&newEmail={dto.NewEmail}&token={encodedToken}";
            await _emailService.SendEmailAsync(dto.NewEmail, "Confirm Email Change", $"<a href='{url}'>Click here to confirm</a>");

            return Ok(new { message = SuccessMessages.ChangeEmailRequest });
        }

        [HttpGet("confirm-email-change")]
        public async Task<IActionResult> ConfirmEmailChange(string userId, string newEmail, string token)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound(new { message = ErrorMessages.InvalidRequest });

            var result = await _userManager.ChangeEmailAsync(user, newEmail, token);
            if (!result.Succeeded)
                return BadRequest(new { message = result.Errors });

            return Ok(new { message = SuccessMessages.ChangeEmailSuccess });
        }
    }
}
