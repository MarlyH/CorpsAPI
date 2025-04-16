using Azure.Core;
using CorpsAPI.DTOs;
using CorpsAPI.Models;
using CorpsAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;

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
        private readonly Dictionary<string, string> _refreshTokenStore = RefreshTokenStore.RefreshTokens;
        private readonly IMemoryCache _otpMemoryCache;

        public AuthController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, EmailService emailService, IMemoryCache otpMemoryCache)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _otpMemoryCache = otpMemoryCache;
        }

        // TODO: all checks against email should be changed to id since email can be updated

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

            if (!result.Succeeded) return BadRequest(result.Errors);

            // generate email verification token
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = WebUtility.UrlEncode(token);
            var confirmationUrl = $"{serverUrl}/api/auth/confirm-email?userId={user.Id}&token={encodedToken}";

            // send email
            await _emailService.SendEmailAsync(user.Email, "Verify your email", 
                $"Confirm your email:\n<a href='{confirmationUrl}'>Click Here!</a>");

            return Ok("Registration successful. Please check your email to confirm your account.");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            // check email
            if (user == null) return Unauthorized(new
            {
                message = "Invalid login credentials.",
                canResend = false
            });

            // check if email is verified
            // TODO: app looks for 401 response, checks for canSend value, and displays a link to the resend-verification endpoint
            if (!user.EmailConfirmed) return Unauthorized(new
            {
                message = "Email not confirmed. Please check your inbox/spam folders for the verification link.",
                canResend = true
            });

            // check password
            var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, false);
            if (!result.Succeeded) return Unauthorized(new
            {
                message = "Invalid login credentials.",
                canResend = false
            });

            // TODO: Test access token gets verified.
            // TODO: Test access token gets renewed.

            // build out credentials for signing tokens
            string secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
            if (secretKey.IsNullOrEmpty()) return Unauthorized("Secret key not found");
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var accessTokenString = GenerateAccessToken(user, credentials);
            var refreshTokenString = GenerateRefreshToken(user, credentials);
            
            return Ok(new
            {
                accessToken = accessTokenString,
                refreshToken = refreshTokenString
            });
        }

        // TODO: implement expiry time
        // TODO: make post? (exposing token with GET)
        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound("User not found.");

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (result.Succeeded) return Ok("Email successfully confirmed.");
            else return BadRequest("Email confirmation failed.");
        }

        [HttpPost("resend-verification")]
        public async Task<IActionResult> ResendVerification([FromBody] ResendEmailDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null) return NotFound("Invalid email address");
            if (user.EmailConfirmed) return BadRequest("Email is already confirmed");

            // generate email verification token
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = WebUtility.UrlEncode(token);
            var confirmationUrl = $"{serverUrl}/api/auth/confirm-email?userId={user.Id}&token={encodedToken}";

            // send email
            // TODO: implement rate limiting, five mins per email per user.
            await _emailService.SendEmailAsync(user.Email, "Verify your email",
                $"Confirm your email:\n<a href='{confirmationUrl}'>Click Here!</a>");

            return Ok("Verificiation email resent. Please check your inbox/spam folders.");
        }

        // TODO: redo 401 return messages
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshDto dto)
        {
            // check for refresh token
            if (string.IsNullOrEmpty(dto.RefreshToken)) return BadRequest("Refresh token is required");
            
            // get signing key
            string secretKeyString = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
            if (string.IsNullOrEmpty(secretKeyString)) return Unauthorized("Secret key not found");
            var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKeyString));
            
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
                    IssuerSigningKey = secretKey,
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
                if (!_refreshTokenStore.TryGetValue(jti, out string? userIdMemory))
                {
                    return Unauthorized("Refresh token not found in memory.");
                }
                // check user id in token matches what is in memory
                if (userIdToken != userIdMemory)
                {
                    return Unauthorized("Token subject mismatch.");
                }
                // and finally remove old token from memory
                _refreshTokenStore.Remove(jti);

                // build out credentials for signing new token
                var credentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);
                if (credentials == null) return Unauthorized("Credentials error.");
                 
                var user = await _userManager.FindByIdAsync(userIdToken);
                if (user == null) return Unauthorized("User not found.");

                // generate new tokens. refresh token added to memory when generated.
                var newAccessToken = GenerateAccessToken(user, credentials);
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
                return Unauthorized("Invalid token.");
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null || !await _userManager.IsEmailConfirmedAsync(user))
            {
                // don't reveal that the user does not exist or email is not confirmed
                return Unauthorized();
            }

            // generate token and 6-digit OTP code.
            var resetPswdToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var otp = new Random().Next(100000, 999999).ToString();

            // send email to user containing the OTP
            await _emailService.SendEmailAsync(user.Email, "Reset Password", $"Your one-time password is:<b>{otp}</b>Enter your code in the app to reset your password.");

            // store token, otp, and email in memory
            _otpMemoryCache.Set(user.Email, otp, TimeSpan.FromMinutes(30));

            // return token to user
            return Ok(new
            {
                resetPswdToken
            });
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto dto)
        {
            // check user and get values from memory to be validated against
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null) return Unauthorized("Email not found");
            if (!_otpMemoryCache.TryGetValue(dto.Email, out string otp)) return Unauthorized("User not found in memory cache");

            // check otp
            if (dto.Otp != otp) return Unauthorized("Provided OTP incorrect");

            // remove from memory
            _otpMemoryCache.Remove(user.Email);

            return Ok("OTP verified");
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null) return Unauthorized();

            // validate token. if valid, update password
            var result = await _userManager.ResetPasswordAsync(user, dto.ResetPswdToken, dto.NewPswd);
            if (!result.Succeeded)
            {
                var errorMessages = string.Join(",\n", result.Errors.Select(e => e.Description));
                return Unauthorized($"Password reset failed:\n{errorMessages}");
            }

            return Ok("Password successfully reset.");
        }

        // TOOD: probably put these in a "JwtService" file or something
        private string GenerateAccessToken(IdentityUser user, SigningCredentials credentials)
        {
            // claims to encode in access token payload
            var accessClaims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                //new Claim("role", "User"),
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
                new Claim(JwtRegisteredClaimNames.Iat, DateTime.Now.ToString())
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
            _refreshTokenStore.Add(jti, user.Id);

            // serialize
            return new JwtSecurityTokenHandler().WriteToken(refreshToken);
        }
    }

}
