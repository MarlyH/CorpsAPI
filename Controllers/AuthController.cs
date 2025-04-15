using Azure.Core;
using CorpsAPI.DTOs;
using CorpsAPI.Models;
using CorpsAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
        private readonly Dictionary<string, string> _refreshTokenStore = RefreshTokenStore.Tokens;

        public AuthController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, EmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDTO dto)
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
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
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
        public async Task<IActionResult> ResendVerification([FromBody] ResendEmailDTO dto)
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
        public async Task<IActionResult> Refresh([FromBody] RefreshDTO dto)
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
