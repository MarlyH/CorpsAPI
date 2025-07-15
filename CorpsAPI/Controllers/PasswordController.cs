using CorpsAPI.Constants;
using CorpsAPI.DTOs.PasswordRecovery;
using CorpsAPI.DTOs.Profile;
using CorpsAPI.Models;
using CorpsAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace CorpsAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PasswordController : ControllerBase
    {
        private readonly EmailService _emailService;
        private readonly IMemoryCache _memoryCache;
        private readonly UserManager<AppUser> _userManager;

        public PasswordController(
            EmailService emailService, 
            IMemoryCache memoryCache, 
            UserManager<AppUser> userManager)
        {
            _emailService = emailService;
            _memoryCache = memoryCache;
            _userManager = userManager;
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null || !user.EmailConfirmed)
                // don't reveal that the user does not exist or email is not confirmed
                return Unauthorized(new { message = ErrorMessages.AccountNotEligible });

            // generate token and 6-digit OTP code.
            var resetPswdToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var otp = new Random().Next(100000, 999999).ToString();

            // send email to user containing the OTP
            await _emailService.SendEmailAsync(
                user.Email,
                "Reset Password",
                $@"
                <div style='
                    background-color: #f9f9f9;
                    padding: 40px 20px;
                    font-family: Helvetica, Arial, sans-serif;
                    text-align: center;
                    color: #333;
                '>
                    <h2 style='margin-bottom: 24px;'>Reset Your Password</h2>

                    <p style='font-size: 16px; margin-bottom: 30px;'>
                        Use the one-time password (OTP) below to reset your account password. This code is valid for 10 minutes.
                    </p>

                    <div style='
                        display: inline-block;
                        font-size: 32px;
                        font-weight: bold;
                        letter-spacing: 6px;
                        color: #ffffff;
                        background-color: #007BFF;
                        padding: 16px 32px;
                        border-radius: 12px;
                        margin-bottom: 30px;
                    '>{otp}</div>

                    <p style='font-size: 14px; color: #777; margin-top: 32px;'>
                        If you didn't request a password reset, please ignore this message.<br>
                        For security, do not share this code with anyone.
                    </p>
                </div>"
            );
            // store OTP in memory
            _memoryCache.Set(user.Email, otp, TimeSpan.FromMinutes(10));

            // return token to client
            return Ok(new
            {
                message = SuccessMessages.PasswordResetOtpSent,
                resetPswdToken
            });
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto dto)
        {
            // check user and get values from memory to be validated against
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null || !user.EmailConfirmed)
                return Unauthorized(new { message = ErrorMessages.AccountNotEligible });
            if (!_memoryCache.TryGetValue(dto.Email, out string otp))
                return Unauthorized(new { message = ErrorMessages.ExpiredOtp });

            // check otp
            if (dto.Otp != otp)
                return BadRequest(new { message = ErrorMessages.IncorrectOtp });

            // remove from memory
            _memoryCache.Remove(user.Email);

            return Ok();
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null || !user.EmailConfirmed)
                return Unauthorized(new { message = ErrorMessages.AccountNotEligible });

            // validate token. if valid, update password
            var result = await _userManager.ResetPasswordAsync(user, dto.ResetPasswordToken, dto.NewPassword);
            if (!result.Succeeded)
            {
                var errorMessages = string.Join(",\n", result.Errors.Select(e => e.Description));
                return BadRequest(new { message = $"Password reset failed:\n{errorMessages}" });
            }

            return Ok(new { message = SuccessMessages.PasswordSuccessfullyReset });
        }

        [HttpPost("change-password")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager},{Roles.Staff},{Roles.User}")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto dto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound(new { message = ErrorMessages.InvalidRequest });

            var result = await _userManager.ChangePasswordAsync(user, dto.OldPassword, dto.NewPassword);

            if (!result.Succeeded)
            {
                var errorMessages = string.Join(",\n", result.Errors.Select(e => e.Description));
                return BadRequest(new { message = "Password change failed:\n" + errorMessages });
            }

            return Ok(new { message = SuccessMessages.PasswordChangeSuccessful });
        }
    }
}
