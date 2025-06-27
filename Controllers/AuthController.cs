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
        // private const string serverUrl = "https://localhost:7125";
        private const string serverUrl = "http://localhost:5133";
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly EmailService _emailService;
        private readonly IMemoryCache _memoryCache;
        private readonly IConfiguration _configuration;

        public AuthController(
            UserManager<AppUser> userManager, 
            SignInManager<AppUser> signInManager, 
            EmailService emailService, 
            IMemoryCache memoryCache,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _memoryCache = memoryCache;
            _configuration = configuration;
        }
        
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

            // Check for duplicate email
            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null)
                return BadRequest(new { message = ErrorMessages.EmailTaken });

            var result = await _userManager.CreateAsync(user, dto.Password);

            // Check if the user was created before assigning the role
            if (!result.Succeeded)
                return BadRequest(new { message = result.Errors.Select(e => e.Description) });

            // Now it's safe to assign a role
            await _userManager.AddToRoleAsync(user, Roles.User);

            // Generate email verification token
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = WebUtility.UrlEncode(token);
            var confirmationUrl = $"{serverUrl}/api/auth/confirm-email?userId={user.Id}&token={encodedToken}";

            // Send email
            await _emailService.SendEmailAsync(user.Email, "Verify your email", 
                $"Confirm your email:\n<a href='{confirmationUrl}'>Click Here!</a>");

            // Store in memory
            _memoryCache.Set($"confirm:{user.Email}", true, TimeSpan.FromDays(1));

            return Ok(new { message = SuccessMessages.RegistrationSuccessful }); //(constant) string SuccessMessages.RegistrationSuccessful = "Registration successful. Please check your email to activate your account.
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
            string secretKey = _configuration["JwtSecretKey"];
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

        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound(ErrorMessages.InvalidRequest);

            var expiredResult = _memoryCache.TryGetValue($"confirm:{user.Email}", out _);
            if (!expiredResult) return BadRequest(ErrorMessages.EmailConfirmationExpired);

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (!result.Succeeded) return BadRequest(ErrorMessages.EmailConfirmationFailed);

            // return custom CSS & HTML email verification message
            var html = @"
                <!DOCTYPE html>
                <html lang='en'>
                <head>
                    <meta charset='UTF-8'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <title>CorpsApp | Email Confirmed</title>
                    <style>
                        body {
                            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                            background-color:rgb(0, 0, 0);
                            display: flex;
                            justify-content: center;
                            align-items: center;
                            height: 100vh;
                            margin: 0;
                        }
                        .card {
                            background-color: rgb(28, 28, 28);
                            padding: 40px;
                            border-radius: 10px;
                            box-shadow: 0 0 10px rgba(255, 255, 255, 0.1);
                            text-align: center;
                        }
                        .card h1 {
                            color:rgb(255, 255, 255);
                            margin-bottom: 20px;
                        }
                        .card p {
                            color: rgb(207, 207, 207);
                            margin-bottom:10px;
                        }
                    </style>
                </head>
                <body>
                    <div class='card'>
                        <h1>Email Verified</h1>
                        <p>Thank you! Your email has been successfully confirmed.</p>
                        <p>Return to the Your Corps App to login.</p>
                    </div>
                </body>
                </html>";

            return Content(html, "text/html");
        }

        [HttpPost("resend-confirmation")]
        public async Task<IActionResult> ResendConfirmation([FromBody] ResendEmailDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                return NotFound(new { message = ErrorMessages.InvalidRequest });

            if (user.EmailConfirmed)
                return BadRequest(new { message = ErrorMessages.EmailAlreadyConfirmed });

            // enforce rate-limiting
            if (_memoryCache.TryGetValue($"resend:{user.Email}", out _))
                return BadRequest(new { message = ErrorMessages.ResendEmailRateLimited });

            // generate email verification token
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = WebUtility.UrlEncode(token);
            var confirmationUrl = $"{serverUrl}/api/auth/confirm-email?userId={user.Id}&token={encodedToken}";

            // send email
            await _emailService.SendEmailAsync(user.Email, "Verify your email",
                $"Confirm your email:\n<a href='{confirmationUrl}'>Click Here!</a>");

            // store in memory, only one email every five minutes per user.
            _memoryCache.Set($"resend:{user.Email}", true, TimeSpan.FromMinutes(5));

            // store in memory, give users a day to confirm
            _memoryCache.Set($"confirm:{user.Email}", true, TimeSpan.FromDays(1));

            return Ok(new { message =  SuccessMessages.EmailConfirmationResent });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshDto dto)
        {
            // check for refresh token
            if (string.IsNullOrEmpty(dto.RefreshToken)) 
                return BadRequest(new { message = ErrorMessages.InvalidRequest });

            // get signing key
            string secretKeyString = _configuration["JwtSecretKey"];
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
                    return NotFound(new { message = ErrorMessages.InvalidRequest });

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
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
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

        [HttpGet("profile")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager},{Roles.Staff},{Roles.User}")]
        public async Task<IActionResult> GetProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound(new { message = ErrorMessages.InvalidRequest });

            return Ok(new
            {
                userName = user.UserName,
                firstName = user.FirstName,
                lastName = user.LastName,
                email = user.Email
            });
        }

        [HttpPatch("profile")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.EventManager},{Roles.Staff},{Roles.User}")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound(new { message = ErrorMessages.InvalidRequest });

            if (!string.IsNullOrEmpty(dto.NewEmail) && dto.NewEmail != user.Email)
            {
                var emailExists = await _userManager.FindByEmailAsync(dto.NewEmail);
                if (emailExists != null) 
                    return BadRequest(new { message = ErrorMessages.EmailTaken });

                user.Email = dto.NewEmail;
            }

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
