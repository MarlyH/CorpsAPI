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

            // BRAND SETTINGS
            const string appName = "Your Corps";
            const string logoUrl = "https://static.wixstatic.com/media/ff8734_0e11ba81866b4340a9ba8d912f1a5423~mv2.png/v1/fill/w_542,h_112,al_c,q_85,usm_0.66_1.00_0.01,enc_avif,quality_auto/YOURCORPS_THIN%20copy.png";
            const string supportEmail = "yourcorps@yourcorps.co.nz";
            const int expiresInMinutes = 10;

            var htmlBody = EmailOTPTemplate.PasswordResetOtpHtml(
                appName: appName,
                logoUrl: logoUrl,
                supportEmail: supportEmail,
                userDisplayName: user.FirstName ?? user.UserName ?? "there",
                otpCode: otp,
                expiresInMinutes: expiresInMinutes
            );

            // send email to user containing the OTP
            await _emailService.SendEmailAsync(
                user.Email,
                $"{appName} – Reset your password",
                htmlBody
            );

            // store OTP in memory
            _memoryCache.Set(user.Email, otp, TimeSpan.FromMinutes(expiresInMinutes));

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
