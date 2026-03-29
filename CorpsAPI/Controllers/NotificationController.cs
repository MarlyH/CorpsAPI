using CorpsAPI.Constants;
using CorpsAPI.DTOs;
using CorpsAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CorpsAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationController : ControllerBase
    {
        private readonly NotificationService _notificationService;

        public NotificationController(NotificationService notificationService)
        {
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

        [HttpPost("broadcast")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> SendBroadcastNotification([FromBody] BroadcastNotificationDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(e => e.Value?.Errors.Count > 0)
                    .SelectMany(e => e.Value!.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToArray();

                return BadRequest(new { message = errors });
            }

            var title = string.IsNullOrWhiteSpace(dto.Title) ? "Your Corps Update" : dto.Title.Trim();
            var message = dto.Message?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(message))
                return BadRequest(new { message = "Message cannot be empty." });

            try
            {
                await _notificationService.SendBroadcastNotificationAsync(title, message);
                return Ok(new { message = "Broadcast notification sent." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Failed to send broadcast notification: {ex.Message}" });
            }
        }
    }
}
