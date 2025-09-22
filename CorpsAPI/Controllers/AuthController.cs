using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization.Metadata;
using CorpsAPI.Constants;
using CorpsAPI.DTOs.Auth;
using CorpsAPI.Models;
using CorpsAPI.Services;
using CorpsAPI.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace CorpsAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly EmailService _emailService;
        private readonly TokenService _tokenService;
        private readonly IMemoryCache _memoryCache;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _context;

        public AuthController(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            EmailService emailService,
            TokenService tokenService,
            IMemoryCache memoryCache,
            IConfiguration configuration,
            AppDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _tokenService = tokenService;
            _memoryCache = memoryCache;
            _configuration = configuration;
            _context = context;
        }
        private static int CalculateAge(DateOnly dob)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var age = today.Year - dob.Year;
            if (today < dob.AddYears(age)) age--;
            return age;
        }
        
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var age = CalculateAge(dto.DateOfBirth);

            // Age-gated validation for medical fields
            if (age < 16)
            {
                if (dto.HasMedicalConditions is null)
                    return BadRequest(new { message = "Please indicate whether the user has medical conditions/allergies." });

                if (dto.HasMedicalConditions == true)
                {
                    if (dto.MedicalConditions is null || dto.MedicalConditions.Count == 0)
                        return BadRequest(new { message = "List at least one medical condition/allergy or set the toggle to No." });

                    // optional: sanitize/validate entries
                    foreach (var mc in dto.MedicalConditions)
                    {
                        if (string.IsNullOrWhiteSpace(mc.Name))
                            return BadRequest(new { message = "Condition name cannot be empty." });
                    }
                }
            }

            // Duplicate email check
            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null)
                return BadRequest(new { message = ErrorMessages.EmailTaken });

            var user = new AppUser
            {
                UserName    = dto.UserName,
                Email       = dto.Email,
                FirstName   = dto.FirstName,
                LastName    = dto.LastName,
                DateOfBirth = dto.DateOfBirth,
                PhoneNumber = dto.PhoneNumber
            };

            var createResult = await _userManager.CreateAsync(user, dto.Password);
            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return ValidationProblem(ModelState);
            }

            await _userManager.AddToRoleAsync(user, Roles.User);

            // Persist medical conditions for <16 registrations
            if (age < 16 && dto.HasMedicalConditions == true && dto.MedicalConditions != null)
            {
                var rows = dto.MedicalConditions
                    .Select(m => new UserMedicalCondition
                    {
                        UserId    = user.Id,
                        Name = m.Name.Trim(),
                        Notes     = string.IsNullOrWhiteSpace(m.Notes) ? null : m.Notes!.Trim()
                    })
                    // de-dup by name if you want:
                    .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                if (rows.Count > 0)
                {
                    _context.UserMedicalConditions.AddRange(rows);
                    await _context.SaveChangesAsync();
                }
            }

            // Email confirm flow (unchanged)
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = WebUtility.UrlEncode(token);
            var serverUrl = _configuration["ServerUrl"];
            var confirmationUrl = $"{serverUrl}/api/auth/confirm-email?userId={user.Id}&token={encodedToken}";
            await _emailService.SendEmailAsync(user.Email, "Verify your email",
                $"Confirm your email:\n<a href='{confirmationUrl}'>Click Here!</a>");

            _memoryCache.Set($"confirm:{user.Email}", true, TimeSpan.FromDays(1));

            return Ok(new { message = SuccessMessages.RegistrationSuccessful });
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

            var accessTokenString = _tokenService.GenerateAccessToken(user, credentials, userRoles);
            var refreshTokenString = _tokenService.GenerateRefreshToken(user, credentials);
            
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

        [HttpPost("resend-confirmation-email")]
        public async Task<IActionResult> ResendConfirmationEmail([FromBody] ResendEmailDto dto)
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
            var serverUrl = _configuration["ServerUrl"];
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

                /*// check token is in memory.
                if (!_memoryCache.TryGetValue(jti, out string? userIdMemory))
                    return Unauthorized(new { message = ErrorMessages.InvalidRequest });

                // check user id in token matches what is in memory
                if (userIdToken != userIdMemory)
                    return Unauthorized(new { message = ErrorMessages.InvalidRequest });

                // and finally remove old token from memory
                _memoryCache.Remove(jti);*/

                // build out credentials for signing new token
                var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
                 
                var user = await _userManager.FindByIdAsync(userIdToken);
                if (user == null)
                    return NotFound(new { message = ErrorMessages.InvalidRequest });

                IList<string> userRoles = await _userManager.GetRolesAsync(user);

                // generate new tokens. refresh token added to memory when generated.
                var newAccessToken = _tokenService.GenerateAccessToken(user, credentials, userRoles);
                var newRefreshToken = _tokenService.GenerateRefreshToken(user, credentials);

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
    }
}
