using CorpsAPI.Constants;
using CorpsAPI.Data;
using CorpsAPI.DTOs;
using CorpsAPI.Models;
using CorpsAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.NotificationHubs;
using System.Security.Claims;

namespace CorpsAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notificationService;

        public NotificationController(AppDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
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

            await _notificationService.RegisterDeviceAsync(dto.DeviceToken, dto.Platform, userId);

            return Ok(new { message = SuccessMessages.DeviceTokenCreateSuccessful });
        }

        /*[HttpPost("send-test")]
        [Authorize]
        public async Task<IActionResult> SendTestNotificationFcmv1()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "Invalid user" });

            try
            {
                await _notificationService.SendFcmV1NotificationAsync(userId, "Test Notification", "If you see this, push is working.");
                return Ok(new { message = "Notification sent" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Failed to send test notification: {ex.Message}" });
            }
        }*/

        // TODO: remove this at some point
        [HttpPost("send-test-generic")]
        [Authorize]
        public async Task<IActionResult> SendTestNotificationGeneric()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "Invalid user" });

            try
            {
                await _notificationService.SendCrossPlatformNotificationAsync(userId, "Cross Platform Test Notification", "If you see this, push is working.");
                return Ok(new { message = "Notification sent" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Failed to send test notification: {ex.Message}" });
            }
        }
    }
}
