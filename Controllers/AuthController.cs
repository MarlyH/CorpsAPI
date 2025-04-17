using Azure.Core;
using CorpsAPI.Constants;
using CorpsAPI.DTOs;
using CorpsAPI.Models;
using CorpsAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization.Metadata;

namespace CorpsAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private const string serverUrl = "https://localhost:7125";
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly EmailService _emailService;
        private readonly IMemoryCache _memoryCache;

        public AuthController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, EmailService emailService, IMemoryCache memoryCache)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _memoryCache = memoryCache;
        }

        // TODO: all checks against email should be changed to id since email can be updated. update endpoint doc wording too.

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var user = new AppUser 
            { 
                UserName = dto.UserName, 
                Email = dto.Email,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                DateOfBirth = dto.DateOfBirth,
            };
            var result = await _userManager.CreateAsync(user, dto.Password);
            await _userManager.AddToRoleAsync(user, Roles.User);

            if (!result.Succeeded)
                return BadRequest(new { message = result.Errors.Select(e => e.Description) });

            // generate email verification token
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = WebUtility.UrlEncode(token);
            var confirmationUrl = $"{serverUrl}/api/auth/confirm-email?userId={user.Id}&token={encodedToken}";

            // send email
            await _emailService.SendEmailAsync(user.Email, "Verify your email", 
                $"Confirm your email:\n<a href='{confirmationUrl}'>Click Here!</a>");

            return Ok(new { message = SuccessMessages.RegistrationSuccessful});
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);

            // check email exists, don't expose email with 404 Not Found code in case of failure
            if (user == null) return Unauthorized(new
            {
                message = ErrorMessages.InvalidCredentials,
                canResend = false
            });

            // try sign in
            var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, false);
            if (!result.Succeeded)
            {
                if (!user.EmailConfirmed)
                    return Unauthorized(new
                    {
                        message = ErrorMessages.EmailNotConfirmed,
                        canResend = true
                    });

                return Unauthorized(new
                {
                    message = ErrorMessages.InvalidCredentials,
                    canResend = false
                });
            }

            // TODO: Test access token gets verified.
            // TODO: Test access token gets renewed.

            // build out credentials for signing tokens
            IList<string> userRoles = await _userManager.GetRolesAsync(user);
            string secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
            if (secretKey.IsNullOrEmpty()) 
                return StatusCode(500, new { message = ErrorMessages.InternalServerError });
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var accessTokenString = GenerateAccessToken(user, credentials, userRoles);
            var refreshTokenString = GenerateRefreshToken(user, credentials);
            
            return Ok(new
            {
                accessToken = accessTokenString,
                refreshToken = refreshTokenString
            });
        }

        // TODO: implement expiry time?
        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound(ErrorMessages.InvalidRequest);

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (!result.Succeeded) return BadRequest(ErrorMessages.EmailConfirmationFailed);

            return Ok(SuccessMessages.EmailConfirmed);
        }

        [HttpPost("resend-verification")]
        public async Task<IActionResult> ResendVerification([FromBody] ResendEmailDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                return NotFound(new { message = ErrorMessages.InvalidRequest });

            if (user.EmailConfirmed)
                return BadRequest(new { message = ErrorMessages.EmailAlreadyConfirmed });

            // generate email verification token
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = WebUtility.UrlEncode(token);
            var confirmationUrl = $"{serverUrl}/api/auth/confirm-email?userId={user.Id}&token={encodedToken}";

            // send email
            // TODO: implement rate limiting, five mins per email per user.
            await _emailService.SendEmailAsync(user.Email, "Verify your email",
                $"Confirm your email:\n<a href='{confirmationUrl}'>Click Here!</a>");

            return Ok(new { message =  SuccessMessages.EmailConfirmationResent });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshDto dto)
        {
            // check for refresh token
            if (string.IsNullOrEmpty(dto.RefreshToken)) 
                return BadRequest(new { message = ErrorMessages.InvalidRequest });
            
            // get signing key
            string secretKeyString = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
            if (string.IsNullOrEmpty(secretKeyString)) 
                return StatusCode(500, new { message = ErrorMessages.InternalServerError });
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKeyString));
            
            try
            {
                // refresh token payload must match validation parameters
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "corps-api-refresh",
                    ValidateAudience = true,
                    ValidAudience = "corps-app-refresh",
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = securityKey,
                    ClockSkew = TimeSpan.Zero // no leeway on expiration time
                };

                // create a token handler to validate the refresh token against the validation params.
                // throws an exception if token is invalid or expired.
                // principal holds all the previous claims so we can extract payload
                var tokenHandler = new JwtSecurityTokenHandler();
                var principalClaims = tokenHandler.ValidateToken(dto.RefreshToken, validationParameters, out SecurityToken validatedToken);

                // extract the jti so we can remove the old token from memory
                var jti = principalClaims.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                // extract the sub claim (userId). Claim comes through as NameIdentifier so can't retrieve it normally like literally every other claim in the payload
                var userIdToken = principalClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                // check token is in memory.
                if (!_memoryCache.TryGetValue(jti, out string? userIdMemory))
                    return Unauthorized(new { message = ErrorMessages.InvalidRequest });

                // check user id in token matches what is in memory
                if (userIdToken != userIdMemory)
                    return Unauthorized(new { message = ErrorMessages.InvalidRequest });

                // and finally remove old token from memory
                _memoryCache.Remove(jti);

                // build out credentials for signing new token
                var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
                 
                var user = await _userManager.FindByIdAsync(userIdToken);
                if (user == null) 
                    return Unauthorized(new { message = ErrorMessages.InvalidRequest });
                IList<string> userRoles = await _userManager.GetRolesAsync(user);

                // generate new tokens. refresh token added to memory when generated.
                var newAccessToken = GenerateAccessToken(user, credentials, userRoles);
                var newRefreshToken = GenerateRefreshToken(user, credentials);

                // return new tokens
                return Ok(new
                {
                    accessToken = newAccessToken,
                    refreshToken = newRefreshToken
                });
            }
            catch (Exception)
            {
                return Unauthorized(new { message = ErrorMessages.InvalidRequest });
            }
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
            await _emailService.SendEmailAsync(user.Email, "Reset Password", $"Your one-time password is:<b>{otp}</b>Enter your code in the app to reset your password.");

            // store email and OTP in memory
            _memoryCache.Set(user.Email, otp, TimeSpan.FromMinutes(30));

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

        private string GenerateAccessToken(AppUser user, SigningCredentials credentials, IList<string> roles)
        {
            // claims to encode in access token payload
            var accessClaims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Role, string.Join(",", roles))
            };

            // build out the access token and add fields into payload with the claims
            var accessToken = new JwtSecurityToken(
                issuer: "corps-api-access",
                audience: "corps-app-access",
                claims: accessClaims,
                expires: DateTime.Now.AddMinutes(15),
                signingCredentials: credentials
                );

            // serialize
            return new JwtSecurityTokenHandler().WriteToken(accessToken);
        }

        private string GenerateRefreshToken(IdentityUser user, SigningCredentials credentials)
        {
            // claims to encode in refresh token payload
            var jti = Guid.NewGuid().ToString();
            var refreshClaims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Jti, jti),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.Now.ToUnixTimeSeconds().ToString())
            };

            // build out the refresh token and add fields into payload with the claims
            var refreshToken = new JwtSecurityToken(
                issuer: "corps-api-refresh",
                audience: "corps-app-refresh",
                claims: refreshClaims,
                expires: DateTime.Now.AddDays(7),
                signingCredentials: credentials
                );

            // store jti in memory
            _memoryCache.Set(jti, user.Id, TimeSpan.FromDays(7));

            // serialize
            return new JwtSecurityTokenHandler().WriteToken(refreshToken);
        }
    }

}
