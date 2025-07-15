using System.Security.Claims;
using CorpsAPI.Constants;
using CorpsAPI.Data;
using CorpsAPI.DTOs;
using CorpsAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CorpsAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationController : ControllerBase
    {
        private readonly AppDbContext _context;
        public NotificationController(AppDbContext context) {
            _context = context;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> RegisterDeviceToken([FromBody] RegisterDeviceTokenDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = ErrorMessages.InvalidRequest });

            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(e => e.Value?.Errors.Count > 0)
                    .SelectMany(e => e.Value!.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToArray();

                return BadRequest(new { message = errors });
            }

            var token = new UserDeviceToken
            {
                UserId = userId,
                Token = dto.DeviceToken,
                Platform = dto.Platform,
            };

            _context.UserDeviceTokens.Add(token);
            await _context.SaveChangesAsync();

            return Ok(new { message = SuccessMessages.DeviceTokenCreateSuccessful });
        }

    }
}
